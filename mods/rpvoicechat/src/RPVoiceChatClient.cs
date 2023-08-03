using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading.Tasks;
using Lidgren.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace rpvoicechat
{
    public class RPVoiceChatClient : RPVoiceChatMod
    {
        MicrophoneManager micManager;
        AudioOutputManager audioOutputManager;

        protected ICoreClientAPI capi;

        private bool audioClientConnected = false;
        private long gameTickId = 0;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            base.StartClientSide(capi);

            // Init microphone and audio output manager
            micManager = new MicrophoneManager(capi);
            audioOutputManager = new AudioOutputManager(capi);
            
            // Init voice chat client
            client = new RPVoiceChatSocketClient(api);

            // Add voice chat client event handlers
            client.OnMessageReceived += OnMessageReceived;
            client.OnClientConnected += VoiceClientConnected;
            client.OnClientDisconnected += VoiceClientDisconnected;

            // Set up clientside handling of game network
            capi.Network.GetChannel("rpvoicechat").SetMessageHandler<ConnectionInfo>(OnConnectionInfo);

            capi.Event.LeftWorld += OnPlayerLeaving;
            capi.Event.PauseResume += OnPauseResume;

            // Initialize gui
            MainConfig configGui = new MainConfig(capi, micManager, audioOutputManager);
            api.Gui.RegisterDialog(new HudIcon(capi, micManager));

            // Set up keybinds
            capi.Input.RegisterHotKey("voicechatMenu", "RPVoice: Config menu", GlKeys.P, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatVoiceLevel", "RPVoice: Change voice volume", GlKeys.Tab, HotkeyType.GUIOrOtherControls, false, false, true);
            capi.Input.RegisterHotKey("voicechatPTT", "RPVoice: Push to talk", GlKeys.CapsLock, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatMute", "RPVoice: Toggle mute", GlKeys.N, HotkeyType.GUIOrOtherControls);

            // Set up keybind event handlers
            capi.Input.SetHotKeyHandler("voicechatMenu", (t1) => 
            {
                configGui.Toggle();
                return true;
            });
            
            capi.Input.SetHotKeyHandler("voicechatVoiceLevel", (t1) =>
            {
                var level = micManager.CycleVoiceLevel();
                capi.ShowChatMessage("RPVoice: Voice level set to " + level.ToString());
                return true;
            });

            capi.Input.SetHotKeyHandler("voicechatMute", (t1) =>
            {
                config.IsMuted = !config.IsMuted;
                ModConfig.Save(capi);
                return true;
            });


            micManager.OnBufferRecorded += (buffer, length, voiceLevel) =>
            {
                audioOutputManager.HandleLoopback(buffer, length, voiceLevel);

                if (audioClientConnected)
                {
                    AudioPacket packet = new AudioPacket()
                    {
                        PlayerId = capi.World.Player.PlayerUID,
                        AudioData = buffer,
                        Length = length,
                        VoiceLevel = voiceLevel
                    };
                    client.SendAudioToServer(packet);
                }
            };
        }

        private void OnPauseResume(bool isPaused)
        {
            // should we stop audio for some reason ?
            // I think even if the game is paused (which should be impossible on a server) we should still play audio
        }

        private void OnMessageReceived(object sender, NetIncomingMessage msg)
        {
            audioOutputManager.HandleAudioPacket(AudioPacket.ReadFromMessage(msg));
        }

        private void VoiceClientConnected(object sender, EventArgs e)
        {
            audioClientConnected = true;
            var status = e as ConnectionStatusUpdate;
            capi.Logger.Debug("[RPVoiceChat - Client] Voice client connected{0}", status?.Reason);

            capi.Event.EnqueueMainThreadTask(() =>
            {
                // Set up game events
                gameTickId = capi.Event.RegisterGameTickListener(OnGameTick, 1);
            }, "register mic update");
        }

        private void VoiceClientDisconnected(object sender, EventArgs e)
        {
            audioClientConnected = false;
            var status = e as ConnectionStatusUpdate;
            capi.Logger.Debug("[RPVoiceChat - Client] Voice client disconnected {0}", status?.Reason);

            // Stop game events
            if (gameTickId != 0)
                capi.Event.EnqueueMainThreadTask(() => { capi.Event.UnregisterGameTickListener(gameTickId); }, "Unregister mic tick");
        }

        private void OnGameTick(float dt)
        {
            if(micManager == null || audioOutputManager == null)
                return;

            micManager.UpdateCaptureAudioSamples();
        }

        private void OnPlayerLeaving()
        {
            client.Close();
        }

        private void OnConnectionInfo(ConnectionInfo packet)
        {
            if (packet == null) return;
            client.ConnectToServer(packet.Address, packet.Port);
            capi.Logger.Debug($"[RPVoiceChat - Client] Connected to server {packet.Address}:{packet.Port}");
        }

        public override void Dispose()
        {
            micManager?.Dispose();
            client?.Dispose();
            client = null;
        }
    }
}
