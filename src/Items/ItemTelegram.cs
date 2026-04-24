using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.Items
{
    public class ItemTelegram : ItemBook
    {

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            // Use base ItemBook logic first (handles readable book info)
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            
            // Add minimal telegram-specific information
            if (inSlot.Itemstack?.Attributes != null)
            {
                string networkUID = inSlot.Itemstack.Attributes.GetString("networkUID", "");
                string networkName = inSlot.Itemstack.Attributes.GetString("networkName", "");
                string networkLabel = !string.IsNullOrWhiteSpace(networkName) ? networkName : networkUID;
                
                if (!string.IsNullOrEmpty(networkLabel))
                {
                    dsc.AppendLine();
                    dsc.AppendLine($"Network: {networkLabel}");
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            // Ensure telegram data is always in the correct format for ItemBook
            if (slot.Itemstack?.Attributes != null)
            {
                string message = slot.Itemstack.Attributes.GetString("message", "");
                string networkUID = slot.Itemstack.Attributes.GetString("networkUID", "");
                string networkName = slot.Itemstack.Attributes.GetString("networkName", "");
                string networkLabel = !string.IsNullOrWhiteSpace(networkName) ? networkName : networkUID;
                string sourceEndpointName = slot.Itemstack.Attributes.GetString("sourceEndpointName", "");
                string targetEndpointName = slot.Itemstack.Attributes.GetString("targetEndpointName", "");
                
                // Always ensure we have text and title attributes
                if (!slot.Itemstack.Attributes.HasAttribute("text"))
                {
                    string text = !string.IsNullOrEmpty(message) ? message : "Empty telegram";
                    string title = "Telegram";
                    
                    if (!string.IsNullOrWhiteSpace(sourceEndpointName))
                    {
                        title += !string.IsNullOrEmpty(networkLabel)
                            ? $" - {sourceEndpointName} (Network {networkLabel})"
                            : $" - {sourceEndpointName}";
                    }
                    else if (!string.IsNullOrWhiteSpace(targetEndpointName))
                    {
                        title += !string.IsNullOrEmpty(networkLabel)
                            ? $" - {targetEndpointName} (Network {networkLabel})"
                            : $" - {targetEndpointName}";
                    }
                    else if (!string.IsNullOrEmpty(networkLabel))
                    {
                        title += $" - Network {networkLabel}";
                    }
                    
                    slot.Itemstack.Attributes.SetString("text", text);
                    slot.Itemstack.Attributes.SetString("title", title);
                }
            }
            
            // Use the base ItemBook logic for readable functionality
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            // Only return the read interaction, not the write interaction
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    MouseButton = EnumMouseButton.Right,
                    ActionLangCode = "heldhelp-read",
                    ShouldApply = (wi, bs, es) => {
                        return isReadable(inSlot) && (inSlot.Itemstack.Attributes.HasAttribute("text") || inSlot.Itemstack.Attributes.HasAttribute("textCodes"));
                    }
                }
            };
        }

    }
}
