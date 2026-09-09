using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Inventory
{
    public class SlotPrinterPaper : SlotRestrictedItemPath
    {
        public SlotPrinterPaper(InventoryBase inventory) : base(inventory, "paperslip", 64) { }

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
    }
}
