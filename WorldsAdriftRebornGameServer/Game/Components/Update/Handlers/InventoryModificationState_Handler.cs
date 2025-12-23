using Bossa.Travellers.Inventory;
using Bossa.Travellers.Player;
using Improbable.Worker.Internal;
using WorldsAdriftRebornGameServer.DLLCommunication;
using WorldsAdriftRebornGameServer.Game.Items;
using WorldsAdriftRebornGameServer.Networking.Wrapper;

namespace WorldsAdriftRebornGameServer.Game.Components.Update.Handlers
{
    [RegisterComponentUpdateHandler]
    internal class InventoryModificationState_Handler : IComponentUpdateHandler<InventoryModificationState,
        InventoryModificationState.Update, InventoryModificationState.Data>
    {
        public InventoryModificationState_Handler() { Init(1082); }

        protected override void Init( uint ComponentId )
        {
            this.ComponentId = ComponentId;
        }

        public static void ApplyWearable(
            ENetPeerHandle player,
            long entityId,
            int itemId,
            bool isLockboxItem,
            bool equip )
        {
            var wearableUtilsState =
                (WearableUtilsState.Update)((WearableUtilsState.Data)
                    ClientObjects.Instance.Dereference(
                        GameState.Instance.ComponentMap[player][entityId][1280]))
                .ToUpdate();

            var playerPropertiesState =
                (PlayerPropertiesState.Update)((PlayerPropertiesState.Data)
                    ClientObjects.Instance.Dereference(
                        GameState.Instance.ComponentMap[player][entityId][1088]))
                .ToUpdate();

            var inventoryState =
                (InventoryState.Update)((InventoryState.Data)
                    ClientObjects.Instance.Dereference(
                        GameState.Instance.ComponentMap[player][entityId][1081]))
                .ToUpdate();

            var inventory = inventoryState.inventoryList.Value;
            var lockbox = inventoryState.lockBoxItems.Value;

            string targetItemTypeId = null;
            string targetCharacterSlot = "None";

            FindTarget(inventory);
            if (targetItemTypeId == null)
                FindTarget(lockbox);
            
            Console.WriteLine($"EQUIP/UNEQUIP ITEM: {targetItemTypeId}");

            if (equip)
            {
                // Clear conflicts globally
                ClearSlotConflicts(inventory);
                ClearSlotConflicts(lockbox);

                // Equip in the correct inventory
                if (isLockboxItem)
                    SetItemSlot(lockbox, targetCharacterSlot);
                else
                    SetItemSlot(inventory, targetCharacterSlot);

                wearableUtilsState
                    .SetItemIds(new Improbable.Collections.List<int> { itemId })
                    .SetHealths(new Improbable.Collections.List<float> { 100f })
                    .SetActive(new Improbable.Collections.List<bool> { true });
            }
            else
            {
                // Unequip from either inventory
                SetItemSlot(inventory, "None");
                SetItemSlot(lockbox, "None");

                wearableUtilsState
                    .SetItemIds(new Improbable.Collections.List<int>())
                    .SetHealths(new Improbable.Collections.List<float>())
                    .SetActive(new Improbable.Collections.List<bool>());
            }

            // CRITICAL ORDER: 1081 before 1088
            SendOPHelper.SendComponentUpdateOp(
                player,
                entityId,
                new List<uint> { 1280, 1081, 1088 },
                new List<object> { wearableUtilsState, inventoryState, playerPropertiesState });
            return;

            // clear slot conflicts in a list
            void ClearSlotConflicts( IList<ScalaSlottedInventoryItem> list )
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    var slot = ItemHelper.GetItem(item.itemTypeId).characterSlot;

                    if (slot != targetCharacterSlot)
                        continue;

                    if (item.itemId == itemId)
                        continue;

                    item.slotType = "None";
                    list[i] = item;
                }
            }

            // set or clear the target item
            void SetItemSlot( IList<ScalaSlottedInventoryItem> list, string slot )
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].itemId != itemId)
                        continue;

                    var item = list[i];
                    item.slotType = slot;
                    list[i] = item;
                    return;
                }
            }

            // find item in an inventory
            void FindTarget( IList<ScalaSlottedInventoryItem> list )
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].itemId != itemId)
                        continue;

                    targetItemTypeId = list[i].itemTypeId;
                    targetCharacterSlot = ItemHelper.GetItem(targetItemTypeId).characterSlot;
                    return;
                }
            }
        }


        public override void HandleUpdate( ENetPeerHandle player, long entityId,
            InventoryModificationState.Update clientComponentUpdate,
            InventoryModificationState.Data serverComponentData )
        {
            clientComponentUpdate.ApplyTo(serverComponentData);

            InventoryModificationState.Update serverComponentUpdate =
                (InventoryModificationState.Update)serverComponentData.ToUpdate();

            for (int j = 0; j < clientComponentUpdate.unequipWearable.Count; j++)
            {
                var equipInfo = clientComponentUpdate.unequipWearable[j];

                ApplyWearable(player, entityId, equipInfo.itemId, equipInfo.isLockboxItem, false);
            }

            for (int j = 0; j < clientComponentUpdate.equipWearable.Count; j++)
            {
                var equipInfo = clientComponentUpdate.equipWearable[j];

                ApplyWearable(player, entityId, equipInfo.itemId, equipInfo.isLockboxItem, true);
            }

            for (int j = 0; j < clientComponentUpdate.equipTool.Count; j++)
            {
                Console.WriteLine("[info] game wants to equip a tool");
                Console.WriteLine("[info] id: " + clientComponentUpdate.equipTool[j].itemId);
            }

            for (int j = 0; j < clientComponentUpdate.craftItem.Count; j++)
            {
                Console.WriteLine("[info] game wants to craft an item");
                Console.WriteLine("[info] inventoryEntityId: " + clientComponentUpdate.craftItem[j].inventoryEntityId);
                Console.WriteLine("[info] itemTypeId: " + clientComponentUpdate.craftItem[j].itemTypeId);
                Console.WriteLine("[info] amount: " + clientComponentUpdate.craftItem[j].amount);
            }

            for (int j = 0; j < clientComponentUpdate.crossInventoryMoveItem.Count; j++)
            {
                Console.WriteLine("[info] game wants to cross inventory move item");
                Console.WriteLine("[info] srcItemId: " + clientComponentUpdate.crossInventoryMoveItem[j].srcItemId);
                Console.WriteLine("[info] xPos: " + clientComponentUpdate.crossInventoryMoveItem[j].xPos);
                Console.WriteLine("[info] yPos: " + clientComponentUpdate.crossInventoryMoveItem[j].yPos);
                Console.WriteLine("[info] rotate: " + clientComponentUpdate.crossInventoryMoveItem[j].rotate);
                Console.WriteLine("[info] srcInventoryEntityId: " +
                                  clientComponentUpdate.crossInventoryMoveItem[j].srcInventoryEntityId);
                Console.WriteLine("[info] destInventoryItemId: " +
                                  clientComponentUpdate.crossInventoryMoveItem[j].destInventoryEntityId);
                Console.WriteLine("[info] isLockBoxItem: " +
                                  clientComponentUpdate.crossInventoryMoveItem[j].isLockboxItem);
            }

            for (int j = 0; j < clientComponentUpdate.moveItem.Count; j++)
            {
                Console.WriteLine("[info] game wants to move an inventory item");
                Console.WriteLine("[info] inventoryEntityId: " + clientComponentUpdate.moveItem[j].inventoryEntityId);
                Console.WriteLine("[info] itemId: " + clientComponentUpdate.moveItem[j].itemId);
                Console.WriteLine("[info] xPos: " + clientComponentUpdate.moveItem[j].xPos);
                Console.WriteLine("[info] yPos: " + clientComponentUpdate.moveItem[j].yPos);
                Console.WriteLine("[info] rotate: " + clientComponentUpdate.moveItem[j].rotate);
                Console.WriteLine("[info] isLockboxItem: " + clientComponentUpdate.moveItem[j].isLockboxItem);
            }

            for (int j = 0; j < clientComponentUpdate.removeFromHotBar.Count; j++)
            {
                Console.WriteLine("[info] game wants to remove from hotbar");
                Console.WriteLine("[info] slotIndex: " + clientComponentUpdate.removeFromHotBar[j].slotIndex);
                Console.WriteLine("[info] isLockboxItem: " + clientComponentUpdate.removeFromHotBar[j].isLockboxItem);
            }

            for (int j = 0; j < clientComponentUpdate.assignToHotBar.Count; j++)
            {
                Console.WriteLine("[info] game wants to assign to hotbar");
                Console.WriteLine("[info] itemId: " + clientComponentUpdate.assignToHotBar[j].itemId);
                Console.WriteLine("[info] slotIndex: " + clientComponentUpdate.assignToHotBar[j].slotIndex);
                Console.WriteLine("[info] isLockboxItem: " + clientComponentUpdate.assignToHotBar[j].isLockboxItem);
            }

            SendOPHelper.SendComponentUpdateOp(player, entityId, new List<uint> { ComponentId },
                new List<object> { serverComponentUpdate });
        }
    }
}
