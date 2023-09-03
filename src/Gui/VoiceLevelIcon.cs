using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class VoiceLevelIcon : HudElement
    {
        const float size = 64;
        static readonly Dictionary<VoiceLevel, string> textureNameByVoiceLevel = new Dictionary<VoiceLevel, string>
        {
            { VoiceLevel.Whispering, "whisper" },
            { VoiceLevel.Talking, "talk" },
            { VoiceLevel.Shouting, "shout" },
        };
        ElementBounds dialogBounds = new ElementBounds() {
            Alignment = EnumDialogArea.RightBottom,
            BothSizing = ElementSizing.Fixed,
            fixedWidth = size,
            fixedHeight = size,
            fixedPaddingX = 10 * 2 + size,
            fixedPaddingY = 10
        };

        VoiceLevel currentVoiceLevel;
        
        private RPVoiceChatConfig _config;

        public VoiceLevelIcon(ICoreClientAPI capi, MicrophoneManager microphoneManager, RPVoiceChatConfig config) : base(capi)
        {
            this._config = config;
            microphoneManager.VoiceLevelUpdated += OnVoiceLevelUpdated;
            currentVoiceLevel = microphoneManager.GetVoiceLevel();
        }

        public override void OnOwnPlayerDataReceived()
        {
            SetupIcon();
        }

        private void OnVoiceLevelUpdated(VoiceLevel voiceLevel)
        {
            currentVoiceLevel = voiceLevel;
            capi.Event.EnqueueMainThreadTask(SetupIcon, "rpvoicechat:VoiceLevelIcon");
        }

        public void SetupIcon()
        {
            if(!_config.IsHUDShown)
            {
                TryClose();
                return;
            }


            string voiceLevel = textureNameByVoiceLevel[currentVoiceLevel];
            SingleComposer = capi.Gui.CreateCompo("rpvcvoicelevelicon", dialogBounds)
                .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation("rpvoicechat", $"textures/gui/{voiceLevel}.png"))
                .Compose();

            TryOpen();
        }
    }
}
