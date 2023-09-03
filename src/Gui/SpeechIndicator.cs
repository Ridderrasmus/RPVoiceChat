using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class SpeechIndicator : HudElement
    {
        RPVoiceChatConfig _config;
        const float size = 64;
        const int silenceTimeout = 160;
        ElementBounds dialogBounds = new ElementBounds() {
            Alignment = EnumDialogArea.RightBottom,
            BothSizing = ElementSizing.Fixed,
            fixedWidth = size,
            fixedHeight = size,
            fixedPaddingX = 10,
            fixedPaddingY = 10
        };

        string voice;
        bool isTalking;
        long? silenceTimer;

        public SpeechIndicator(ICoreClientAPI capi, MicrophoneManager microphoneManager) : base(capi)
        {
            _config = ModConfig.Config;

            GuiDialogCreateCharacterPatch.OnCharacterSelection += bindToMainThread(UpdateVoice);
            microphoneManager.ClientStartTalking += bindToMainThread(OnClientStartTalking);
            microphoneManager.ClientStopTalking += bindToMainThread(OnClientStopTalking);
            ModConfig.ConfigUpdated += bindToMainThread(OnConfigUpdate);
        }

        public override void OnOwnPlayerDataReceived()
        {
            UpdateVoice();
        }

        public void UpdateVoice()
        {
            voice = capi.World.Player?.Entity.talkUtil.soundName.GetName() ?? voice;
            SetupIcon();
        }

        private Action bindToMainThread(Action function)
        {
            return () => { capi.Event.EnqueueMainThreadTask(function, "rpvoicechat:SpeechIndicator"); };
        }

        private void OnClientStartTalking()
        {
            if (silenceTimer != null)
            {
                capi.Event.UnregisterCallback((long)silenceTimer);
                silenceTimer = null;
            }
            isTalking = true;
            UpdateDisplay();
        }

        private void OnClientStopTalking()
        {
            if (silenceTimer != null) return;
            silenceTimer = capi.Event.RegisterCallback(OnSilence, silenceTimeout);
        }

        private void OnSilence(float _)
        {
            silenceTimer = null;
            isTalking = false;
            UpdateDisplay();
        }

        private void OnConfigUpdate()
        {
            SetupIcon();
        }

        private void UpdateDisplay()
        {
            bool shouldDisplay = (_config.IsMuted || isTalking) && _config.IsHUDShown;
            bool successful = shouldDisplay ? TryOpen() : TryClose();

            if (!successful) bindToMainThread(UpdateDisplay)();
        }

        public void SetupIcon()
        {
            SingleComposer = capi.Gui.CreateCompo("rpvcspeechindicator", dialogBounds)
                .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation("rpvoicechat", "textures/gui/" + voice + ".png"))
                .AddIf(_config.IsMuted)
                .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation("rpvoicechat", "textures/gui/muted.png"))
                .EndIf()
                .Compose();

            UpdateDisplay();
        }

        public override void Dispose()
        {
            if (silenceTimer == null) return;
            capi.Event.UnregisterCallback((long)silenceTimer);
        }
    }
}
