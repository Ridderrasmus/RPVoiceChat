﻿using RPVoiceChat.Audio;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
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
        ElementBounds dialogBounds = new ElementBounds()
        {
            Alignment = EnumDialogArea.RightBottom,
            BothSizing = ElementSizing.Fixed,
            fixedWidth = size,
            fixedHeight = size,
            fixedPaddingX = 10 * 2 + size,
            fixedPaddingY = 10
        };

        VoiceLevel currentVoiceLevel;

        public VoiceLevelIcon(ICoreClientAPI capi, MicrophoneManager microphoneManager) : base(capi)
        {
            currentVoiceLevel = microphoneManager.GetVoiceLevel();

            microphoneManager.VoiceLevelUpdated += OnVoiceLevelUpdated;
            ModConfig.ConfigUpdated += bindToMainThread(OnConfigUpdate);
        }

        public override void OnOwnPlayerDataReceived()
        {
            SetupIcon();
        }

        private Action bindToMainThread(Action function)
        {
            return () => { capi.Event.EnqueueMainThreadTask(function, "rpvoicechat:VoiceLevelIcon"); };
        }

        private void OnVoiceLevelUpdated(VoiceLevel voiceLevel)
        {
            currentVoiceLevel = voiceLevel;
            bindToMainThread(SetupIcon)();
        }

        private void OnConfigUpdate()
        {
            SetupIcon();
        }

        private void UpdateDisplay()
        {
            bool shouldDisplay = ModConfig.Config.IsHUDShown;
            bool successful = shouldDisplay ? TryOpen() : TryClose();

            if (!successful) bindToMainThread(UpdateDisplay)();
        }

        public void SetupIcon()
        {
            string voiceLevel = textureNameByVoiceLevel[currentVoiceLevel];
            SingleComposer = capi.Gui.CreateCompo("rpvcvoicelevelicon", dialogBounds)
                .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation("rpvoicechat", $"textures/gui/{voiceLevel}.png"))
                .Compose();

            UpdateDisplay();
        }
    }
}
