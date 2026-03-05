using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Inventory
{
    public class SlotLucerneFat : ItemSlot
    {
        public SlotLucerneFat(InventoryBase inventory) : base(inventory)
        {
            MaxSlotStackSize = 64;
        }

        /// <summary>Same pattern as SlotPrinterPaper: only restrict what can be inserted, no CanTakeFrom.</summary>
        public override bool CanHold(ItemSlot sourceSlot)
        {
            return sourceSlot.Itemstack?.Collectible?.Code?.Path == "fat";
        }

    }
}
