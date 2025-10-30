#nullable enable
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Inventory
{
    public class InventoryPrinter : InventoryGeneric
    {
        public SlotPrinterPaper PaperSlot => (SlotPrinterPaper)slots[0];
        public SlotPrinterTelegram[] TelegramSlots => new SlotPrinterTelegram[] 
        { 
            (SlotPrinterTelegram)slots[1], (SlotPrinterTelegram)slots[2], (SlotPrinterTelegram)slots[3],
            (SlotPrinterTelegram)slots[4], (SlotPrinterTelegram)slots[5], (SlotPrinterTelegram)slots[6],
            (SlotPrinterTelegram)slots[7], (SlotPrinterTelegram)slots[8], (SlotPrinterTelegram)slots[9]
        };

        public InventoryPrinter() : base(10, "printer-temp", "temp", null, OnNewSlot)
        {
            // Default constructor for deserialization
            // Will be properly initialized later
        }


        public InventoryPrinter(ICoreAPI api, BlockPos? pos = null) 
            : base(10, "printer-" + (pos?.ToString() ?? "fake"), pos?.ToString() ?? "-fake", api, OnNewSlot)
        {
            Pos = pos;
        }

        private static ItemSlot OnNewSlot(int slotId, InventoryGeneric self)
        {
            return slotId switch
            {
                0 => new SlotPrinterPaper(self),
                >= 1 and <= 9 => new SlotPrinterTelegram(self),
                _ => throw new System.ArgumentOutOfRangeException(nameof(slotId), "InventoryPrinter should have exactly 10 slots")
            };
        }
    }
}