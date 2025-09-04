using AIGraph;
using Hikaria.Core;
using Hikaria.Core.Interfaces;
using Hikaria.DropItem.Handlers;
using LevelGeneration;
using Localization;
using SNetwork;
using TheArchive.Core.Attributes.Feature;
using TheArchive.Core.Attributes.Feature.Patches;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Core.FeaturesAPI.Settings;
using TheArchive.Loader;
using UnityEngine;

namespace Hikaria.DropItem.Features
{
    [EnableFeatureByDefault]
    [DisallowInGameToggle]
    [HideInModSettings]
    public class DropItem : Feature, IOnRecallComplete
    {
        public override string Name => "放置物品";

        public override void Init()
        {
            GameEventAPI.RegisterListener(this);

            LoaderWrapper.ClassInjector.RegisterTypeInIl2Cpp<LG_WeakResourceContainer_Slot>();
        }

        public override void OnFeatureSettingChanged(FeatureSetting setting)
        {
            if (CurrentGameState < (int)eGameStateName.InLevel)
                return;
        }

        public override void OnGameStateChanged(int state)
        {
            if (state != (int)eGameStateName.InLevel)
                return;

            foreach (var slot in UnityEngine.Object.FindObjectsOfType<LG_WeakResourceContainer_Slot>())
            {
                slot.UpdateInteractionActive();
            }
        }

        public void OnRecallComplete(eBufferType bufferType)
        {
            if (CurrentGameState < (int)eGameStateName.InLevel)
                return;

            LG_WeakResourceContainer_Slot.RemoveAllItems();

            var slots = UnityEngine.Object.FindObjectsOfType<LG_WeakResourceContainer_Slot>();

            foreach (var slot in slots)
            {
                slot.OnPrepareForRecall();
            }

            foreach (var itemSync in UnityEngine.Object.FindObjectsOfType<LG_PickupItem_Sync>())
            {
                var item = itemSync.item;
                if (item == null)
                    continue;
                var state = itemSync.GetCurrentState();
                if (state.status != ePickupItemStatus.PlacedInLevel)
                    continue;
                if (!LG_WeakResourceContainer_Slot.TryFindSlot(itemSync.transform.position, out var slot))
                    continue;
                slot.AddItem(itemSync);
            }

            foreach (var slot in slots)
            {
                slot.UpdateInteractionActive();
            }
        }

        [ArchivePatch(typeof(PlayerInventoryLocal), nameof(PlayerInventoryLocal.DoWieldItem))]
        private class PlayerInventoryLocal__DoWieldItem__Patch
        {
            private static void Postfix(PlayerInventoryLocal __instance)
            {
                if (!__instance.AllowedToWieldItem)
                    return;
                if (DropItemManager.IsInteractDropItem)
                {
                    if (DropItemManager.CurrentSelectedSlot.IsValidItemForDrop(__instance.WieldedItem))
                    {
                        DropItemManager.SpawnItemGhost(DropItemManager.CurrentSelectedSlot, __instance.Owner);
                        GuiManager.InteractionLayer.SetInteractPrompt(string.Format(Text.Get(864U), __instance.WieldedItem?.PublicName),
                            string.Format(Text.Get(827U), InputMapper.GetBindingName(InputAction.Use)), ePUIMessageStyle.Default);
                    }
                }
            }
        }

        [ArchivePatch(typeof(LG_WeakResourceContainer), nameof(LG_WeakResourceContainer.Setup))]
        private class LG_WeakResourceContainer__Setup__Patch
        {
            private static void Postfix(LG_WeakResourceContainer __instance)
            {
                var storage = __instance.m_storage?.TryCast<LG_ResourceContainer_Storage>();
                if (storage == null)
                    return;
                var sync = __instance.m_sync?.TryCast<LG_ResourceContainer_Sync>();
                if (sync == null)
                    return;

                int index = 0;
                foreach (var sslot in storage.m_storageSlots)
                {
                    GameObject go = new GameObject($"ResourceContainerSlot_{index}")
                    {
                        layer = LayerManager.LAYER_INTERACTION
                    };
                    go.transform.SetParent(__instance.transform);
                    var slot = go.AddComponent<LG_WeakResourceContainer_Slot>();
                    slot.Setup(__instance, sync, sslot, index);
                    index++;
                }
            }
        }

        [ArchivePatch(typeof(LG_PickupItem_Sync), nameof(LG_PickupItem_Sync.Setup))]
        private class LG_PickupItem_Sync__Setup__Patch
        {
            private static void Postfix(LG_PickupItem_Sync __instance)
            {
                var state = __instance.GetCurrentState();
                if (state.status != ePickupItemStatus.PlacedInLevel)
                    return;

                if (!LG_WeakResourceContainer_Slot.TryFindSlot(__instance.transform.position, out var slot))
                    return;

                slot.AddItem(__instance, true);
            }
        }

        [ArchivePatch(typeof(LG_PickupItem_Sync), nameof(LG_PickupItem_Sync.SetStateFromFactory))]
        private class LG_PickupItem_Sync__SetStateFromFactory__Patch
        {
            private static void Postfix(LG_PickupItem_Sync __instance)
            {
                var state = __instance.GetCurrentState();
                if (state.status != ePickupItemStatus.PlacedInLevel)
                    return;

                if (!LG_WeakResourceContainer_Slot.TryFindSlot(__instance.transform.position, out var slot))
                    return;

                slot.AddItem(__instance, true);
            }
        }

        [ArchivePatch(typeof(LG_PickupItem_Sync), nameof(LG_PickupItem_Sync.OnStateChange))]
        private class LG_PickupItem_Sync__OnStateChange__Patch
        {
            private static void Prefix(LG_PickupItem_Sync __instance, pPickupItemState newState, bool isRecall)
            {
                if (newState.updateCustomDataOnly)
                    return;
                if (isRecall)
                    return;

                if (newState.status == ePickupItemStatus.PlacedInLevel)
                {
                    if (LG_WeakResourceContainer_Slot.TryFindSlot(newState.placement.position, out var slot))
                    {
                        slot.AddItem(__instance);
                        var item = __instance.item.TryCast<ItemInLevel>();
                        if (item != null)
                            item.container = slot.Storage;
                    }
                }
                else if (newState.status == ePickupItemStatus.PickedUp)
                {
                    if (LG_WeakResourceContainer_Slot.TryFindSlot(__instance, out var slot))
                    {
                        slot.RemoveItem();
                        var item = __instance.item.TryCast<ItemInLevel>();
                        if (item != null)
                            item.container = null;
                    }
                }
            }

            private static void Postfix(LG_PickupItem_Sync __instance, pPickupItemState newState)
            {
                ItemCuller component = __instance.GetComponent<ItemCuller>();
                if (component?.CullBucket != null)
                {
                    component.CullBucket.NeedsShadowRefresh = true;
                    component.CullBucket.SetDirtyCMDBuffer();
                }

                // 修正SpawnNode为物品所在CourseNode，因为TerminalPing基于SpawnNode
                if (newState.status == ePickupItemStatus.PlacedInLevel)
                {
                    var terminalItem = __instance.item.GetComponentInChildren<iTerminalItem>();
                    if (terminalItem != null && newState.placement.node.TryGet(out var node))
                        terminalItem.SpawnNode = node;
                }
            }
        }

        [ArchivePatch(typeof(ItemSpawnManager), nameof(ItemSpawnManager.SpawnItem))]
        private class ItemSpawnManager__SpawnItem__Patch
        {
            private static void Postfix(global::Item __result)
            {
                var itemInLevel = __result?.TryCast<ItemInLevel>();
                if (itemInLevel == null)
                    return;
                if (itemInLevel.CourseNode != null)
                    return;

                if (itemInLevel.internalSync.GetCurrentState().status != ePickupItemStatus.PlacedInLevel)
                    return;

                if (itemInLevel.Get_pItemData().originCourseNode.TryGet(out var node))
                {
                    itemInLevel.CourseNode = node;
                    return;
                }
                var terminalItem = itemInLevel.GetComponent<iTerminalItem>();
                if (terminalItem != null)
                {
                    itemInLevel.CourseNode = terminalItem.SpawnNode;
                    return;
                }
                var pickupItem = itemInLevel.transform.parent?.parent?.GetComponentInChildren<LG_PickupItem>();
                if (pickupItem != null)
                {
                    itemInLevel.CourseNode = pickupItem.SpawnNode;
                    return;
                }
            }
        }

        [ArchivePatch(typeof(LG_ResourceContainer_Storage), nameof(LG_ResourceContainer_Storage.SetSpawnNode))]
        private class LG_ResourceContainer_Storage__SetSpawnNode__Patch
        {
            private static void Postfix(LG_ResourceContainer_Storage __instance, GameObject obj, AIG_CourseNode spawnNode)
            {
                foreach (var item in obj.GetComponentsInChildren<ItemInLevel>())
                {
                    if (item.CourseNode == null && item.internalSync.GetCurrentState().status == ePickupItemStatus.PlacedInLevel)
                    {
                        item.CourseNode = spawnNode;
                    }
                }
            }
        }
    }
}
