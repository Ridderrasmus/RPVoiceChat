using System;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Inventory
{
    public class SlotPrinterTelegram : SlotRestrictedItemPath
    {
        public SlotPrinterTelegram(InventoryBase inventory) : base(inventory, "telegram", 1) { }

        public bool TryStoreTelegram(string message, string networkUID = "", string networkName = "", string sourceEndpointName = "", string targetEndpointName = "")
        {
            if (Empty)
            {
                ItemStack telegramStack = new ItemStack(
                    inventory.Api.World.GetItem(new AssetLocation("rpvoicechat:telegram")));

                telegramStack.Attributes.SetString("message", message);
                telegramStack.Attributes.SetLong("timestamp", (long)inventory.Api.World.Calendar.TotalDays);
                if (!string.IsNullOrEmpty(networkUID))
                    telegramStack.Attributes.SetString("networkUID", networkUID);
                if (!string.IsNullOrWhiteSpace(networkName))
                    telegramStack.Attributes.SetString("networkName", networkName);
                if (!string.IsNullOrWhiteSpace(sourceEndpointName))
                    telegramStack.Attributes.SetString("sourceEndpointName", sourceEndpointName);
                if (!string.IsNullOrWhiteSpace(targetEndpointName))
                    telegramStack.Attributes.SetString("targetEndpointName", targetEndpointName);

                telegramStack.Attributes.SetString("text", message);
                telegramStack.Attributes.SetString("title", CreateTelegramTitle(networkUID, networkName, sourceEndpointName, targetEndpointName));

                string description = CreateTelegramDescription(message, networkUID, networkName, sourceEndpointName, targetEndpointName);
                telegramStack.Attributes.SetString("description", description);

                Itemstack = telegramStack;
                MarkDirty();
                return true;
            }
            return false;
        }

        private static string CreateTelegramDescription(string message, string networkUID, string networkName, string sourceEndpointName, string targetEndpointName)
        {
            string networkLabel = ResolveNetworkLabel(networkUID, networkName);
            if (!string.IsNullOrWhiteSpace(sourceEndpointName))
            {
                return $"Network: {sourceEndpointName} ({networkLabel})\nMessage: {message}";
            }

            if (!string.IsNullOrWhiteSpace(targetEndpointName))
            {
                return $"Network: {targetEndpointName} ({networkLabel})\nMessage: {message}";
            }

            return $"Network: {networkLabel}\nMessage: {message}";
        }

        private static string CreateTelegramTitle(string networkUID, string networkName, string sourceEndpointName, string targetEndpointName)
        {
            string networkLabel = ResolveNetworkLabel(networkUID, networkName);
            if (!string.IsNullOrWhiteSpace(sourceEndpointName))
            {
                if (!string.IsNullOrWhiteSpace(networkLabel))
                {
                    return $"Telegram - {sourceEndpointName} (Network {networkLabel})";
                }

                return $"Telegram - {sourceEndpointName}";
            }

            if (!string.IsNullOrWhiteSpace(targetEndpointName))
            {
                if (!string.IsNullOrWhiteSpace(networkLabel))
                {
                    return $"Telegram - {targetEndpointName} (Network {networkLabel})";
                }

                return $"Telegram - {targetEndpointName}";
            }

            if (!string.IsNullOrWhiteSpace(networkLabel))
            {
                return $"Telegram - Network {networkLabel}";
            }

            return "Telegram";
        }

        private static string ResolveNetworkLabel(string networkUID, string networkName)
        {
            if (!string.IsNullOrWhiteSpace(networkName))
            {
                return networkName;
            }
            if (!string.IsNullOrWhiteSpace(networkUID))
            {
                return networkUID;
            }

            return "Unknown";
        }
    }
}
