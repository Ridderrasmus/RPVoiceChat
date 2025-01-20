using RPVoiceChat.Audio;
using RPVoiceChat.Client;
using RPVoiceChat.DB;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking;
using RPVoiceChat.Systems;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class RPVoiceChatClient : RPVoiceChatMod
    {
        private ICoreClientAPI capi;
        private ClientSettingsRepository clientSettingsRepository;
        private MicrophoneManager micManager;
        private AudioOutputManager audioOutputManager;
        private PlayerNetworkClient client;
        private GuiManager guiManager;
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

            WireNetworkHandler.RegisterClientside(api);

            // Sneak in native dlls
            EmbeddedDllClass.ExtractEmbeddedDlls();
            EmbeddedDllClass.LoadDll("RNNoise.dll");

            // Init data repositories
            clientSettingsRepository = new ClientSettingsRepository(capi.Logger);

            // Init microphone and audio output managers
            micManager = new MicrophoneManager(capi);
            audioOutputManager = new AudioOutputManager(capi, clientSettingsRepository);

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
            guiManager = new GuiManager(capi, micManager, audioOutputManager, clientSettingsRepository);

            // Set up keybinds
            capi.Input.RegisterHotKey("voicechatMenu", UIUtils.I18n("Hotkey.ModMenu"), GlKeys.P, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatVoiceLevel", UIUtils.I18n("Hotkey.VoiceLevel"), GlKeys.Tab, HotkeyType.GUIOrOtherControls, false, false, true);
            capi.Input.RegisterHotKey("voicechatPTT", UIUtils.I18n("Hotkey.PTT"), GlKeys.CapsLock, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatMute", UIUtils.I18n("Hotkey.Mute"), GlKeys.N, HotkeyType.GUIOrOtherControls);
            capi.Event.KeyUp += this.Event_KeyUp;

            // Set up keybind event handlers
            capi.Input.SetHotKeyHandler("voicechatMenu", _ =>
            {
                if (voiceMenuPressed) return true;
                voiceMenuPressed = true;

                guiManager.modMenuDialog.Toggle();
                return true;
            });

            capi.Input.SetHotKeyHandler("voicechatVoiceLevel", _ =>
            {
                if (voiceLevelPressed) return true;
                voiceLevelPressed = true;

                micManager.CycleVoiceLevel();
                return true;
            });

            capi.Input.SetHotKeyHandler("voicechatMute", _ =>
            {
                if (mutePressed) return true;
                mutePressed = true;

                ClientSettings.IsMuted = !ClientSettings.IsMuted;
                capi.Event.PushEvent("rpvoicechat:hudUpdate");
                ClientSettings.Save();
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
            guiManager.firstLaunchDialog.ShowIfNecessary();
            isReady = true;
        }

        private void Event_KeyUp(KeyEvent e)
        {
            int HotkeyCode(string hotkeyName) => capi.Input.HotKeys[hotkeyName].CurrentMapping.KeyCode;

            if (e.KeyCode == HotkeyCode("voicechatMenu")) voiceMenuPressed = false;
            if (e.KeyCode == HotkeyCode("voicechatVoiceLevel")) voiceLevelPressed = false;
            if (e.KeyCode == HotkeyCode("voicechatMute")) mutePressed = false;
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

            if (micManager.AudioWizardActive) return;
            client.SendAudioToServer(packet);
        }

        public override void Dispose()
        {
            ClientSettings.Save();
            micManager?.Dispose();
            audioOutputManager?.Dispose();
            client?.Dispose();
            guiManager?.Dispose();
            clientSettingsRepository?.Dispose();

            capi.Event.KeyUp -= this.Event_KeyUp;
            capi.Event.LevelFinalize -= OnLoad;
        }
    }
}
