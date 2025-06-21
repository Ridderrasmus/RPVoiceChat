using RPVoiceChat.Audio;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui.CustomElements
{
    internal class GroupMemberElement : GuiElementContainer
    {
        public GroupMemberElement(ICoreClientAPI capi, ElementBounds bounds, string playeruid, AudioOutputManager audioOutputManager) : base(capi, bounds)
        {
            bool isTalking = (playeruid == "Player2") ? audioOutputManager.IsPlayerTalking(playeruid) : true;

            // Speech indicator
            var speechIndicator = new GuiElementStaticText(capi, isTalking ? "•" : " ", EnumTextOrientation.Left, ElementBounds.Fixed(0, 0, 10, bounds.fixedHeight), CairoFont.WhiteDetailText());
            speechIndicator.Bounds.Alignment = EnumDialogArea.LeftMiddle;
            speechIndicator.Bounds.fixedWidth = 10;
            speechIndicator.Bounds.fixedHeight = bounds.fixedHeight;
            speechIndicator.Bounds.fixedPaddingX = 0;
            speechIndicator.Bounds.fixedPaddingY = 0;
            
            Add(speechIndicator);


            // Player name
            var playerName = capi.World.PlayerByUid(playeruid)?.PlayerName ?? playeruid;
            var nameLabel = new GuiElementStaticText(capi, playerName, EnumTextOrientation.Left, ElementBounds.Fixed(speechIndicator.Bounds.fixedWidth, 0, bounds.fixedWidth - speechIndicator.Bounds.fixedWidth, bounds.fixedHeight), CairoFont.WhiteDetailText());
            nameLabel.Bounds.Alignment = EnumDialogArea.LeftMiddle;


            Add(nameLabel);



        }

    }
}
