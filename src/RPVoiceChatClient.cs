using RPVoiceChat.Audio;
using RPVoiceChat.Client;
using RPVoiceChat.DB;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class RPVoiceChatClient : RPVoiceChatMod
    {
        private ClientSettingsRepository clientSettingsRepository;
        private MicrophoneManager micManager;
        private AudioOutputManager audioOutputManager;
        private PatchManager patchManager;
        private PlayerNetworkClient client;

        protected ICoreClientAPI capi;

        private ModMenuDialog configGui;

        private bool isReady = false;
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

            // Sneak in native dlls
            EmbeddedDllClass.ExtractEmbeddedDlls();
            EmbeddedDllClass.LoadDll("RNNoise.dll");

            // Init data repositories
            clientSettingsRepository = new ClientSettingsRepository(capi.Logger);

            // Init microphone, audio output and harmony patch managers
            micManager = new MicrophoneManager(capi);
            audioOutputManager = new AudioOutputManager(capi, clientSettingsRepository);
            patchManager = new PatchManager(modID);
            patchManager.Patch();

            // Init voice chat client
            bool forwardPorts = !config.ManualPortForwarding;
            var networkTransports = new List<INetworkClient>()
            {
                new UDPNetworkClient(forwardPorts),
                new TCPNetworkClient(),
                new NativeNetworkClient(capi)
            };
            client = new PlayerNetworkClient(capi, networkTransports);

            // Initialize gui
            configGui = new ModMenuDialog(capi, micManager, audioOutputManager, clientSettingsRepository);
            capi.Gui.RegisterDialog(new SpeechIndicator(capi, micManager));
            capi.Gui.RegisterDialog(new VoiceLevelIcon(capi, micManager));
            new PlayerNameTagRenderer(capi, audioOutputManager);

            // Set up keybinds
            capi.Input.RegisterHotKey("voicechatMenu", UIUtils.I18n("Hotkey.ModMenu"), GlKeys.P, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatVoiceLevel", UIUtils.I18n("Hotkey.VoiceLevel"), GlKeys.Tab, HotkeyType.GUIOrOtherControls, false, false, true);
            capi.Input.RegisterHotKey("voicechatPTT", UIUtils.I18n("Hotkey.PTT"), GlKeys.CapsLock, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatMute", UIUtils.I18n("Hotkey.Mute"), GlKeys.N, HotkeyType.GUIOrOtherControls);
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
                capi.Event.PushEvent("rpvoicechat:hudUpdate");
                return true;
            });

            capi.Event.LevelFinalize += OnLoad;
        }

        private void OnLoad()
        {
            client.OnAudioReceived += OnAudioReceived;
            micManager.OnBufferRecorded += OnBufferRecorded;
            micManager.Launch();
            audioOutputManager.Launch();
            isReady = true;
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
            if (!isReady) return;
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
            configGui?.Dispose();
            clientSettingsRepository?.Dispose();
        }
    }
}
