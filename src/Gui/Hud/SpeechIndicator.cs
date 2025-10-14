using RPVoiceChat.Audio;
using RPVoiceChat.Config;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    public class SpeechIndicator : HudElement
    {
        private const float size = 64;
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
        private VoiceLevel currentVoiceLevel;

        public SpeechIndicator(ICoreClientAPI capi, MicrophoneManager microphoneManager) : base(capi)
        {
            audioInputManager = microphoneManager;
            currentVoiceLevel = microphoneManager.GetVoiceLevel();

            GuiDialogCreateCharacterPatch.OnCharacterSelection += bindToMainThread(UpdateVoiceType);
            microphoneManager.TransmissionStateChanged += bindToMainThread(UpdateDisplay);
            microphoneManager.VoiceLevelUpdated += OnVoiceLevelUpdated;
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

        private void OnVoiceLevelUpdated(VoiceLevel voiceLevel)
        {
            currentVoiceLevel = voiceLevel;
            bindToMainThread(SetupIcon)();
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
            bool shouldDisplay;
            
            if (ModConfig.ClientConfig.IsMinimalHud)
            {
                // In minimal mode, only show when talking (or muted)
                shouldDisplay = (ModConfig.ClientConfig.IsMuted || isTalking) && ModConfig.ClientConfig.ShowHud;
            }
            else
            {
                // In normal mode, show when talking or when muted
                shouldDisplay = (ModConfig.ClientConfig.IsMuted || isTalking) && ModConfig.ClientConfig.ShowHud;
            }
            
            bool successful = shouldDisplay ? TryOpen() : TryClose();

            if (!successful) bindToMainThread(UpdateDisplay)();
        }

        public void SetupIcon()
        {
            // In minimal mode, always show the minimal indicator
            if (ModConfig.ClientConfig.IsMinimalHud)
            {
                // Choose color based on microphone manager voice level
                string colorIcon = currentVoiceLevel switch
                {
                    VoiceLevel.Whispering => "minimal-blue.png",
                    VoiceLevel.Talking => "minimal-green.png", 
                    VoiceLevel.Shouting => "minimal-red.png",
                    _ => "minimal-green.png" // Default to green for talk
                };

                SingleComposer = capi.Gui.CreateCompo("rpvcspeechindicator", dialogBounds)
                    .AddImage(ElementBounds.Fixed(16, 16, 32, 32), new AssetLocation(RPVoiceChatMod.modID, "textures/gui/" + colorIcon))
                    .AddIf(ModConfig.ClientConfig.IsMuted)
                    .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation(RPVoiceChatMod.modID, "textures/gui/muted.png"))
                    .EndIf()
                    .Compose();
            }
            else
            {
                // Normal mode - show voice type icons
                string voiceIcon = new AssetLocation(RPVoiceChatMod.modID, "textures/gui/" + voiceType + ".png");
                IAsset asset = capi.Assets.TryGet(voiceIcon, false);
                if (asset == null) 
                {
                    // Display an icon by default if voiceType not exists. Typically, with custom voice mods.
                    voiceIcon = new AssetLocation(RPVoiceChatMod.modID, "textures/gui/megaphone.png");
                }

                SingleComposer = capi.Gui.CreateCompo("rpvcspeechindicator", dialogBounds)
                    .AddImage(ElementBounds.Fixed(0, 0, size, size), voiceIcon)
                    .AddIf(ModConfig.ClientConfig.IsMuted)
                    .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation(RPVoiceChatMod.modID, "textures/gui/muted.png"))
                    .EndIf()
                    .Compose();
            }

            UpdateDisplay();
        }
    }
}
