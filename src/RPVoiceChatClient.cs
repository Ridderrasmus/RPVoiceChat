using RPVoiceChat.Client;
using RPVoiceChat.Networking;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class RPVoiceChatClient : RPVoiceChatMod
    {
        private MicrophoneManager micManager;
        private AudioOutputManager audioOutputManager;
        private PatchManager patchManager;
        private PlayerNetworkClient client;

        protected ICoreClientAPI capi;

        private MainConfig configGui;

        private bool mutePressed = false;
        private bool voiceMenuPressed = false;
        private bool voiceLevelPressed = false;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            // Init microphone, audio output and harmony patch managers
            micManager = new MicrophoneManager(capi);
            audioOutputManager = new AudioOutputManager(capi);
            patchManager = new PatchManager(modID);
            patchManager.Patch();

            // Init voice chat client
            var networkClient = new NativeNetworkClient(api);
            client = new PlayerNetworkClient(api, networkClient);

            // Initialize gui
            configGui = new MainConfig(capi, micManager, audioOutputManager);
            api.Gui.RegisterDialog(new SpeechIndicator(capi, micManager));
            api.Gui.RegisterDialog(new VoiceLevelIcon(capi, micManager));

            // Set up keybinds
            capi.Input.RegisterHotKey("voicechatMenu", "RPVoice: Config menu", GlKeys.P, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatVoiceLevel", "RPVoice: Change speech volume", GlKeys.Tab, HotkeyType.GUIOrOtherControls, false, false, true);
            capi.Input.RegisterHotKey("voicechatPTT", "RPVoice: Push to talk", GlKeys.CapsLock, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatMute", "RPVoice: Toggle mute", GlKeys.N, HotkeyType.GUIOrOtherControls);
            capi.Event.KeyUp += Event_KeyUp;

            // Set up keybind event handlers
            capi.Input.SetHotKeyHandler("voicechatMenu", (t1) =>
            {
                if (voiceMenuPressed)
                    return true;

                voiceMenuPressed = true;

                configGui.Toggle();
                return true;
            });

            capi.Input.SetHotKeyHandler("voicechatVoiceLevel", (t1) =>
            {
                if (voiceLevelPressed)
                    return true;

                voiceLevelPressed = true;

                micManager.CycleVoiceLevel();
                return true;
            });

            capi.Input.SetHotKeyHandler("voicechatMute", (t1) =>
            {
                if (mutePressed)
                    return true;

                mutePressed = true;

                config.IsMuted = !config.IsMuted;
                ModConfig.Save(capi);
                return true;
            });


            client.OnAudioReceived += OnAudioReceived;
            micManager.OnBufferRecorded += OnBufferRecorded;
            capi.Event.LevelFinalize += OnLoad;
        }

        private void OnLoad()
        {
            micManager.Launch();
            audioOutputManager.Launch();
        }

        private void Event_KeyUp(KeyEvent e)
        {

            if (e.KeyCode == capi.Input.HotKeys["voicechatMenu"].CurrentMapping.KeyCode)
                voiceMenuPressed = false;
            else if (e.KeyCode == capi.Input.HotKeys["voicechatVoiceLevel"].CurrentMapping.KeyCode)
                voiceLevelPressed = false;
            else if (e.KeyCode == capi.Input.HotKeys["voicechatMute"].CurrentMapping.KeyCode)
                mutePressed = false;

        }

        private void OnAudioReceived(AudioPacket packet)
        {
            audioOutputManager.HandleAudioPacket(packet);
        }

        private void OnBufferRecorded(byte[] buffer, int length, VoiceLevel voiceLevel)
        {
            if (buffer == null) return;

            AudioPacket packet = new AudioPacket()
            {
                PlayerId = capi.World.Player.PlayerUID,
                AudioData = buffer,
                Length = length,
                VoiceLevel = voiceLevel
            };
            audioOutputManager.HandleLoopback(packet);
            client.SendAudioToServer(packet);
        }

        public override void Dispose()
        {
            micManager?.Dispose();
            patchManager?.Dispose();
            //client?.Dispose();
            configGui.Dispose();
            //client = null;
        }
    }
}
