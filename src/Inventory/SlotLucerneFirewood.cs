using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Inventory
{
    public class SlotLucerneFirewood : ItemSlot
    {
        public SlotLucerneFirewood(InventoryBase inventory) : base(inventory)
        {
            MaxSlotStackSize = 32;
        }

        /// <summary>Same pattern as SlotPrinterPaper: only restrict what can be inserted, no CanTakeFrom.</summary>
        public override bool CanHold(ItemSlot sourceSlot)
        {
            return sourceSlot.Itemstack?.Collectible?.Code?.Path == "firewood";
        }

    }
}
