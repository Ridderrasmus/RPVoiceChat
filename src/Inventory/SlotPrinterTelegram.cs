using System;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Inventory
{
    public class SlotPrinterTelegram : SlotRestrictedItemPath
    {
        public SlotPrinterTelegram(InventoryBase inventory) : base(inventory, "telegram", 1) { }

        public bool TryStoreTelegram(string message, string networkUID = "")
        {
            if (Empty)
            {
                ItemStack telegramStack = new ItemStack(
                    inventory.Api.World.GetItem(new AssetLocation("rpvoicechat:telegram")));

                telegramStack.Attributes.SetString("message", message);
                telegramStack.Attributes.SetLong("timestamp", (long)inventory.Api.World.Calendar.TotalDays);
                if (!string.IsNullOrEmpty(networkUID))
                    telegramStack.Attributes.SetString("networkUID", networkUID);

                string description = CreateTelegramDescription(message, networkUID);
                telegramStack.Attributes.SetString("description", description);

                Itemstack = telegramStack;
                MarkDirty();
                return true;
            }
            return false;
        }

        private static string CreateTelegramDescription(string message, string networkUID) =>
            $"Network: {networkUID}\nMessage: {message}";
    }
}
