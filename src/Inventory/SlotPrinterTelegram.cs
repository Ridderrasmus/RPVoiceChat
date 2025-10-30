using System;
using Vintagestory.API.Common;
using Vintagestory.API.Client;

namespace RPVoiceChat.GameContent.Inventory
{
    public class SlotPrinterTelegram : ItemSlot
    {
        public SlotPrinterTelegram(InventoryBase inventory) : base(inventory)
        {
            MaxSlotStackSize = 1;
        }

        public bool TryStoreTelegram(string message, string networkUID = "")
        {
            if (Empty)
            {
                ItemStack telegramStack = new ItemStack(
                    inventory.Api.World.GetItem(new AssetLocation("rpvoicechat:telegram"))
                );
                
                // Store message content
                telegramStack.Attributes.SetString("message", message);
                
                // Store timestamp (current world time)
                telegramStack.Attributes.SetLong("timestamp", (long)inventory.Api.World.Calendar.TotalDays);
                
                // Store network UID if provided
                if (!string.IsNullOrEmpty(networkUID))
                {
                    telegramStack.Attributes.SetString("networkUID", networkUID);
                }
                
                // Create localized description with network info and timestamp
                string description = CreateTelegramDescription(message, networkUID);
                telegramStack.Attributes.SetString("description", description);

                Itemstack = telegramStack;
                MarkDirty();
                return true;
            }
            return false;
        }

        private string CreateTelegramDescription(string message, string networkUID)
        {
            // Create simple description with network info
            string description = $"Network: {networkUID}\nMessage: {message}";
            
            return description;
        }

        public override bool CanHold(ItemSlot sourceSlot)
        {
            return sourceSlot.Itemstack?.Collectible.Code?.Path == "telegram";
        }
    }
}