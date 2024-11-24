using LevelGeneration;
using Localization;
using Player;
using UnityEngine;

namespace Hikaria.DropItem.Handlers
{
    public static class DropItemManager
    {
        public static bool IsInteractDropItem { get; private set; }

        public static LG_WeakResourceContainer_Slot CurrentSelectedSlot { get; private set; }

        public static void OnSelectedChange(LG_WeakResourceContainer_Slot slot, PlayerAgent agent, bool selected)
        {
            if (selected)
            {
                CurrentSelectedSlot = slot;
                IsInteractDropItem = true;
                SpawnItemGhost(slot, agent);
                GuiManager.InteractionLayer.SetInteractPrompt(string.Format(Text.Get(864U), agent.Inventory.WieldedItem?.PublicName),
                     string.Format(Text.Get(827U), InputMapper.GetBindingName(InputAction.Use)), ePUIMessageStyle.Default);

                if (agent?.IsLocallyOwned ?? false)
                {
                    agent.Sync.SendGenericInteract(pGenericInteractAnimation.TypeEnum.GiveResource, false);
                }
            }
            else
            {
                CurrentSelectedSlot = null;
                IsInteractDropItem = false;
                DespawnItemGhost();
            }
        }

        public static void TriggerInteractionAction(LG_WeakResourceContainer_Slot slot, PlayerAgent source)
        {
            if (source == null)
                return;
            if (!PlayerBackpackManager.TryGetBackpack(source.Owner, out var backpack))
                return;
            var wieldItem = source.Inventory.WieldedItem;
            if (wieldItem == null || !PlayerBackpackManager.TryGetItemInLevelFromItemData(wieldItem.Get_pItemData(), out var item))
                return;
            var itemInLevel = item.TryCast<ItemInLevel>();
            if (itemInLevel == null)
                return;
            var wieldSlot = source.Inventory.WieldedSlot;
            if (!slot.TryGetTransform(wieldSlot, out var tf))
                return;
            var itemSync = itemInLevel.GetSyncComponent();
            if (itemSync == null)
                return;
            pItemData_Custom customData = itemSync.GetCustomData();
            customData.ammo = backpack.AmmoStorage.GetInventorySlotAmmo(wieldSlot).AmmoInPack;
            itemSync.AttemptPickupInteraction(ePickupItemInteractionType.Place, source.Owner, customData, tf.position, tf.rotation, slot.SpawnNode, false, false);
        }

        public static bool PlayerCanInteract(LG_WeakResourceContainer_Slot slot, PlayerAgent source)
        {
            if (!slot.IsContainerOpen || slot.IsSlotInUse)
                return false;

            return slot.IsValidItemForDrop(source.Inventory.WieldedItem);
        }

        public static void SpawnItemGhost(LG_WeakResourceContainer_Slot slot, PlayerAgent agent)
        {
            DespawnItemGhost();
            var wieldItem = agent?.Inventory?.WieldedItem;
            if (wieldItem == null)
                return;
            var lookup = ItemSpawnManager.m_loadedPrefabsPerItemMode[(int)ItemMode.Pickup];
            var crc = wieldItem.Get_pItemData().itemID_gearCRC;
            if (!lookup.ContainsKey(crc))
                return;
            var list = lookup[crc];
            if (list == null || list.Count < 1)
                return;
            var prefab = list[0];
            if (prefab == null)
                return;
            if (!slot.TryGetTransform(agent.Inventory.WieldedSlot, out var tf))
                return;
            s_itemGhost = UnityEngine.Object.Instantiate(prefab, tf.position, tf.rotation);
            foreach (Renderer renderer in s_itemGhost.GetComponentsInChildren<Renderer>())
            {
                foreach (Material material in renderer.materials)
                {
                    material.shader = Shader.Find("Transparent/Diffuse");
                    material.color = Color.black.AlphaMultiplied(0.25f);
                }
            }
        }

        public static void DespawnItemGhost()
        {
            if (s_itemGhost != null)
            {
                UnityEngine.Object.Destroy(s_itemGhost);
                s_itemGhost = null;
            }
        }

        private static GameObject s_itemGhost;
    }
}
