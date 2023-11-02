using RPVoiceChat.Audio;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    public class SpeechIndicator : HudElement
    {
        private const float size = 64;
        private RPVoiceChatConfig config;
        private MicrophoneManager audioInputManager;
        private ElementBounds dialogBounds = new ElementBounds()
        {
            Alignment = EnumDialogArea.RightBottom,
            BothSizing = ElementSizing.Fixed,
            fixedWidth = size,
            fixedHeight = size,
            fixedPaddingX = 10,
            fixedPaddingY = 10
        };
        private string voiceType;

        public SpeechIndicator(ICoreClientAPI capi, MicrophoneManager microphoneManager) : base(capi)
        {
            config = ModConfig.Config;
            audioInputManager = microphoneManager;

            GuiDialogCreateCharacterPatch.OnCharacterSelection += bindToMainThread(UpdateVoiceType);
            microphoneManager.TransmissionStateChanged += bindToMainThread(UpdateDisplay);
            capi.Event.RegisterEventBusListener(OnHudUpdate, 0.5, "rpvoicechat:hudUpdate");
        }

        public override void OnOwnPlayerDataReceived()
        {
            UpdateVoiceType();
        }

        private void UpdateVoiceType()
        {
            voiceType = capi.World.Player?.Entity.talkUtil.soundName.GetName() ?? voiceType;
            SetupIcon();
        }

        private Action bindToMainThread(Action function)
        {
            return () => { capi.Event.EnqueueMainThreadTask(function, "rpvoicechat:SpeechIndicator"); };
        }

        private void OnHudUpdate(string _, ref EnumHandling __, object ___)
        {
            bindToMainThread(SetupIcon)();
        }

        private void UpdateDisplay()
        {
            bool isTalking = audioInputManager.Transmitting;
            bool shouldDisplay = (config.IsMuted || isTalking) && config.IsHUDShown;
            bool successful = shouldDisplay ? TryOpen() : TryClose();

            if (!successful) bindToMainThread(UpdateDisplay)();
        }

        public void SetupIcon()
        {
            SingleComposer = capi.Gui.CreateCompo("rpvcspeechindicator", dialogBounds)
                .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation("rpvoicechat", "textures/gui/" + voiceType + ".png"))
                .AddIf(config.IsMuted)
                .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation("rpvoicechat", "textures/gui/muted.png"))
                .EndIf()
                .Compose();

            UpdateDisplay();
        }
    }
}
