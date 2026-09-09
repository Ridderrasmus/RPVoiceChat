using RPVoiceChat.Audio;
using RPVoiceChat.Client;
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
        private bool isVoiceBanned = false;
        private VoiceLevel lastBuiltVoiceLevel;
        private string lastBuiltVoiceType;
        private bool lastBuiltMinimalHud;
        private bool lastBuiltMuted;
        private bool lastBuiltBanned;
        private bool lastShouldDisplay;

        public SpeechIndicator(ICoreClientAPI capi, MicrophoneManager microphoneManager) : base(capi)
        {
            audioInputManager = microphoneManager;
            currentVoiceLevel = microphoneManager.GetVoiceLevel();

            GuiDialogCreateCharacterPatch.OnCharacterSelection += bindToMainThread(UpdateVoiceType);
            microphoneManager.TransmissionStateChanged += bindToMainThread(UpdateDisplay);
            microphoneManager.VoiceLevelUpdated += OnVoiceLevelUpdated;
            capi.Event.RegisterEventBusListener(OnHudUpdate, 0.5, "rpvoicechat:hudUpdate");
            capi.Event.RegisterEventBusListener(OnVoiceBanUpdate, 0.5, "rpvoicechat:voiceBanUpdate");
            
            // Check if the local player is banned on startup
            CheckVoiceBanStatus();
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
            bindToMainThread(UpdateDisplay)();
        }

        private void OnVoiceBanUpdate(string _, ref EnumHandling __, object ___)
        {
            CheckVoiceBanStatus();
            bindToMainThread(SetupIcon)();
        }

        public override void Dispose()
        {
            SingleComposer?.Dispose();
            base.Dispose();
        }

        private void CheckVoiceBanStatus()
        {
            if (RPVoiceChatClient.VoiceBanManagerInstance != null && capi.World.Player != null)
            {
                isVoiceBanned = RPVoiceChatClient.VoiceBanManagerInstance.IsPlayerBanned(capi.World.Player.PlayerUID);
            }
        }

        private void UpdateDisplay()
        {
            bool isTalking = audioInputManager.Transmitting;
            bool shouldDisplay;
            
            // Display if banned, muted, or talking
            if (ModConfig.ClientConfig.IsMinimalHud)
            {
                // In minimal mode, only show when talking (or muted or banned)
                shouldDisplay = (ModConfig.ClientConfig.IsMuted || isVoiceBanned || isTalking) && ModConfig.ClientConfig.ShowHud;
            }
            else
            {
                // In normal mode, show when talking or when muted or banned
                shouldDisplay = (ModConfig.ClientConfig.IsMuted || isVoiceBanned || isTalking) && ModConfig.ClientConfig.ShowHud;
            }

            if (shouldDisplay == lastShouldDisplay && IsOpened() == shouldDisplay)
            {
                return;
            }

            lastShouldDisplay = shouldDisplay;
            
            bool successful = shouldDisplay ? TryOpen() : TryClose();

            if (!successful) bindToMainThread(UpdateDisplay)();
        }

        public void SetupIcon()
        {
            bool minimalHud = ModConfig.ClientConfig.IsMinimalHud;
            bool isMuted = ModConfig.ClientConfig.IsMuted;
            if (SingleComposer != null
                && currentVoiceLevel == lastBuiltVoiceLevel
                && voiceType == lastBuiltVoiceType
                && minimalHud == lastBuiltMinimalHud
                && isMuted == lastBuiltMuted
                && isVoiceBanned == lastBuiltBanned)
            {
                UpdateDisplay();
                return;
            }

            lastBuiltVoiceLevel = currentVoiceLevel;
            lastBuiltVoiceType = voiceType;
            lastBuiltMinimalHud = minimalHud;
            lastBuiltMuted = isMuted;
            lastBuiltBanned = isVoiceBanned;

            if (IsOpened())
            {
                TryClose();
            }

            // In minimal mode, always show the minimal indicator
            if (minimalHud)
            {
                // Choose color based on microphone manager voice level
                string colorIcon = currentVoiceLevel switch
                {
                    VoiceLevel.Whispering => "minimal-blue.png",
                    VoiceLevel.Talking => "minimal-green.png", 
                    VoiceLevel.Shouting => "minimal-red.png",
                    _ => "minimal-green.png" // Default to green for talk
                };

                SingleComposer?.Dispose();
                SingleComposer = capi.Gui.CreateCompo("rpvcspeechindicator", dialogBounds)
                    .AddImage(ElementBounds.Fixed(16, 16, 32, 32), new AssetLocation(RPVoiceChatMod.modID, "textures/gui/" + colorIcon))
                    .AddIf(isVoiceBanned)
                    .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation(RPVoiceChatMod.modID, "textures/gui/banned.png"))
                    .EndIf()
                    .AddIf(ModConfig.ClientConfig.IsMuted && !isVoiceBanned)
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

                SingleComposer?.Dispose();
                SingleComposer = capi.Gui.CreateCompo("rpvcspeechindicator", dialogBounds)
                    .AddImage(ElementBounds.Fixed(0, 0, size, size), voiceIcon)
                    .AddIf(isVoiceBanned)
                    .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation(RPVoiceChatMod.modID, "textures/gui/banned.png"))
                    .EndIf()
                    .AddIf(ModConfig.ClientConfig.IsMuted && !isVoiceBanned)
                    .AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation(RPVoiceChatMod.modID, "textures/gui/muted.png"))
                    .EndIf()
                    .Compose();
            }

            UpdateDisplay();
        }
    }
}
