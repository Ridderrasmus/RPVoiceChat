using RPVoiceChat.Audio;
using RPVoiceChat.Client;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking;
using System;
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

        private ModMenuDialog configGui;

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
            ClientSettings.Init(capi);

            // Init audio context, microphone, audio output and harmony patch managers
            micManager = new MicrophoneManager(capi);
            audioOutputManager = new AudioOutputManager(capi);
            patchManager = new PatchManager(modID);
            patchManager.Patch();

            // Init voice chat client
            var mainClient = new UDPNetworkClient();
            if (config.ManualPortForwarding) mainClient.TogglePortForwarding(false);
            var backupClient = new NativeNetworkClient(capi);
            client = new PlayerNetworkClient(capi, mainClient, backupClient);

            // Initialize gui
            configGui = new ModMenuDialog(capi, micManager, audioOutputManager);
            capi.Gui.RegisterDialog(new SpeechIndicator(capi, micManager));
            capi.Gui.RegisterDialog(new VoiceLevelIcon(capi, micManager));

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

        private void OnBufferRecorded(AudioData audioData)
        {
            if (audioData.data == null) return;

            string sender = capi.World.Player.PlayerUID;
            var sequenceNumber = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AudioPacket packet = new AudioPacket(sender, audioData, sequenceNumber);
            audioOutputManager.HandleLoopback(packet);
            client.SendAudioToServer(packet);
        }

        public override void Dispose()
        {
            micManager?.Dispose();
            audioOutputManager?.Dispose();
            patchManager?.Dispose();
            client?.Dispose();
            configGui.Dispose();
            ClientSettings.Dispose();
        }
    }
}
