using AIGraph;
using LevelGeneration;
using Player;
using UnityEngine;

namespace Hikaria.DropItem.Handlers
{
    public sealed class LG_WeakResourceContainer_Slot : MonoBehaviour
    {
        public void Setup(LG_WeakResourceContainer container, LG_ResourceContainer_Sync sync, StorageSlot slot, int slotIndex)
        {
            m_resourceContainer = container;
            if (!s_ContainerSlotsLookup.TryGetValue(m_resourceContainer, out var slotsLookup))
            {
                slotsLookup = new();
                s_ContainerSlotsLookup[container] = slotsLookup;
            }
            slotsLookup[slotIndex] = this;

            m_slot = slot;
            m_slotIndex = slotIndex;

            if (slot.ResourcePack != null)
                m_allowedSlots.Add(InventorySlot.ResourcePack);
            if (slot.Consumable != null)
                m_allowedSlots.Add(InventorySlot.Consumable);
            if (slot.Keycard != null)
                m_allowedSlots.Add(InventorySlot.InPocket);

            if (m_resourceContainer.m_isLocker)
            {
                var slotInfo = SlotInfo.LockerSlotInfo[slotIndex];
                gameObject.transform.localRotation = Quaternion.identity;
                gameObject.transform.localPosition = slotInfo.LocalPosition;
                gameObject.transform.localScale = slotInfo.LocalScale;
            }
            else
            {
                var slotInfo = SlotInfo.BoxSlotInfo[slotIndex];
                gameObject.transform.localRotation = Quaternion.identity;
                gameObject.transform.localPosition = slotInfo.LocalPosition;
                gameObject.transform.localScale = slotInfo.LocalScale;
            }

            m_interactionDropItem = GetComponent<Interact_Timed>() ?? gameObject.AddComponent<Interact_Timed>();
            m_interactionDropItem.InteractDuration = 0.4f;
            m_interactionDropItem.InteractionMessage = string.Empty;
            m_interactionDropItem.m_colliderToOwn = GetComponent<BoxCollider>() ?? gameObject.AddComponent<BoxCollider>();
            m_interactionDropItem.ExternalPlayerCanInteract = new Func<PlayerAgent, bool>((player) => { return DropItemManager.PlayerCanInteract(this, player); });
            m_interactionDropItem.OnInteractionSelected = new Action<PlayerAgent, bool>((player, state) => { DropItemManager.OnSelectedChange(this, player, state); });
            m_interactionDropItem.OnInteractionTriggered = new Action<PlayerAgent>((player) => { DropItemManager.TriggerInteractionAction(this, player); });
            m_interactionDropItem.OnlyActiveWhenLookingStraightAt = true;
            m_interactionDropItem.SetActive(true);
            sync.OnSyncStateChange += new Action<eResourceContainerStatus, bool>((status, isRecall) =>
            {
                if (status == eResourceContainerStatus.Open)
                    m_interactionDropItem.SetActive(!IsSlotInUse);
                else if (status == eResourceContainerStatus.Closed)
                    m_interactionDropItem.SetActive(false);
            });
        }

        private void OnDestroy()
        {
            RemoveItem(true);
            DropItemManager.DespawnItemGhost();
            if (s_ContainerSlotsLookup.TryGetValue(m_resourceContainer, out var lookup))
            {
                lookup.Remove(m_slotIndex);
                if (lookup.Count < 1)
                    s_ContainerSlotsLookup.Remove(m_resourceContainer);
            }
        }

        public void OnPrepareForRecall()
        {
            m_interactionDropItem.SetActive(true);
        }

        public void UpdateInteractionActive()
        {
            m_interactionDropItem.SetActive(IsContainerOpen && !IsSlotInUse);
        }

        public void AddItem(LG_PickupItem_Sync item, bool isSetup = false)
        {
            AddItem(item.gameObject.GetInstanceID());

            // 避免出现游离物品，游离物品不会被销毁
            if (!isSetup)
            {
                var itemSlot = item.item.TryCast<ArtifactPickup_Core>() != null ? InventorySlot.InPocket : item.item.Get_pItemData().slot;
                if (TryGetTransform(itemSlot, out var tf))
                    item.transform.SetParent(tf);
            }
        }

        private void AddItem(int instanceID)
        {
            if (!IsSlotInUse)
            {
                m_hasItemInSlot = true;
                m_itemInSlot = instanceID;
                s_SlotItemLookup[this] = m_itemInSlot;
                s_ItemSlotLookup[m_itemInSlot] = this;
                m_interactionDropItem.SetActive(false);
            }
        }

        public void RemoveItem(bool isDestroy = false)
        {
            if (IsSlotInUse)
            {
                m_hasItemInSlot = false;
                s_SlotItemLookup.Remove(this);
                s_ItemSlotLookup.Remove(m_itemInSlot);
                m_itemInSlot = 0;
            }
            if (!isDestroy)
                m_interactionDropItem.SetActive(IsContainerOpen && !IsSlotInUse);
        }

        public static void RemoveAllItems()
        {
            foreach (var slot in s_SlotItemLookup.Keys)
            {
                slot.RemoveItem();
            }
        }

        public bool TryGetTransform(InventorySlot slot, out Transform transform)
        {
            if (!IsValidInventorySlot(slot))
            {
                transform = null;
                return false;
            }

            switch (slot)
            {
                case InventorySlot.ResourcePack:
                    transform = m_slot.ResourcePack;
                    return true;
                case InventorySlot.Consumable:
                    transform = m_slot.Consumable;
                    return true;
            }

            transform = null;
            return false;
        }

        public static bool TryFindSlot(Vector3 pos, out LG_WeakResourceContainer_Slot slot)
        {
            foreach (Collider collider in Physics.OverlapSphere(pos, 0.01f, LayerManager.MASK_PLAYER_INTERACT_SPHERE))
            {
                slot = collider.gameObject.GetComponent<LG_WeakResourceContainer_Slot>();
                if (slot != null)
                {
                    if (!slot.IsSlotInUse)
                        return true;
                }
            }
            slot = null;
            return false;
        }

        public static bool TryFindSlot(LG_PickupItem_Sync itemSync, out LG_WeakResourceContainer_Slot slot)
        {
            if (!s_ItemSlotLookup.TryGetValue(itemSync.gameObject.GetInstanceID(), out slot) && !TryFindSlot(itemSync.transform.position, out slot))
                return false;
            return true;
        }

        public bool IsValidItemForDrop(ItemEquippable item) => IsValidInventorySlot(item?.ItemDataBlock?.inventorySlot ?? InventorySlot.None);

        public bool IsValidInventorySlot(InventorySlot slot) => m_allowedSlots.Contains(slot);

        public bool IsContainerOpen => m_resourceContainer.ISOpen || (m_resourceContainer?.m_graphics?.TryCast<LG_WeakResourceContainer_Graphics>()?.m_status == eResourceContainerStatus.Open);

        public bool IsSlotInUse => m_hasItemInSlot && m_itemInSlot != 0;

        public AIG_CourseNode SpawnNode => m_resourceContainer.SpawnNode;

        private static readonly Dictionary<LG_WeakResourceContainer_Slot, int> s_SlotItemLookup = new();
        private static readonly Dictionary<LG_WeakResourceContainer, Dictionary<int, LG_WeakResourceContainer_Slot>> s_ContainerSlotsLookup = new();
        private static readonly Dictionary<int, LG_WeakResourceContainer_Slot> s_ItemSlotLookup = new();

        private readonly HashSet<InventorySlot> m_allowedSlots = new();
        private LG_WeakResourceContainer m_resourceContainer;
        private StorageSlot m_slot;
        private int m_slotIndex;
        private bool m_hasItemInSlot = false;
        private int m_itemInSlot = 0;

        public float InteractDuration { get => m_interactionDropItem.InteractDuration; set => m_interactionDropItem.InteractDuration = value; }

        private Interact_Timed m_interactionDropItem;

        internal class SlotInfo
        {
            public SlotInfo(Vector3 position, Vector3 scale)
            {
                LocalPosition = position;
                LocalScale = scale;
            }


            public static readonly SlotInfo[] LockerSlotInfo = new SlotInfo[]
            {
                new SlotInfo(new Vector3(-0.724f, -0.252f, 1.754f), new Vector3(0.42f, 0.433f, 0.3f)),
                new SlotInfo(new Vector3(-0.672f, -0.252f, 1.454f), new Vector3(0.5f, 0.433f, 0.25f)),
                new SlotInfo(new Vector3(-0.672f, -0.252f, 1.154f), new Vector3(0.5f, 0.433f, 0.25f)),
                new SlotInfo(new Vector3(-0.672f, -0.252f, 0.854f), new Vector3(0.5f, 0.433f, 0.25f)),
                new SlotInfo(new Vector3(-0.672f, -0.252f, 0.354f), new Vector3(0.5f, 0.433f, 0.67f)),
                new SlotInfo(new Vector3(-0.276f, -0.252f, 1.754f), new Vector3(0.42f, 0.433f, 0.3f))
            };

            public static readonly SlotInfo[] BoxSlotInfo = new SlotInfo[]
            {
                new SlotInfo(new Vector3(0.009f, 0f, 0.163f), new Vector3(0.33f, 0.54f, 0.323f)),
                new SlotInfo(new Vector3(-0.343f, 0f, 0.163f), new Vector3(0.34f, 0.54f, 0.323f)),
                new SlotInfo(new Vector3(0.358f, 0f, 0.163f), new Vector3(0.33f, 0.54f, 0.323f))
            };

            public readonly Vector3 LocalPosition;

            public readonly Vector3 LocalScale;
        }
    }
}