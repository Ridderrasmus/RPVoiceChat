using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Inventory
{
    public class SlotPrinterPaper : ItemSlot
    {
        public SlotPrinterPaper(InventoryBase inventory) : base(inventory)
        {
            MaxSlotStackSize = 64;
        }

        public bool TryConsumePaperSlip()
        {
            if (!Empty && Itemstack.StackSize > 0)
            {
                TakeOut(1);
                MarkDirty();
                return true;
            }
            return false;
        }

        public override bool CanHold(ItemSlot sourceSlot)
        {
            return sourceSlot.Itemstack?.Collectible.Code?.Path == "paperslip";
        }
    }
}