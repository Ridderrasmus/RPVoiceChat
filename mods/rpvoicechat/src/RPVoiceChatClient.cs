using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace rpvoicechat
{
    public class RPVoiceChatClient : RPVoiceChatMod
    {
        MicrophoneManager micManager;
        AudioOutputManager audioOutputManager;

        private bool audioClientConnected = false;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            base.StartClientSide(capi);

            var world = capi.World;
            Type t = world.GetType();
            var s = t.AssemblyQualifiedName;
            // Init microphone and audio output manager
            micManager = new MicrophoneManager(capi);
            audioOutputManager = new AudioOutputManager(capi);
            
            // Init voice chat client
            client = new RPVoiceChatSocketClient(api);

            // Add voice chat client event handlers
            client.OnMessageReceived += (sender, msg) =>
            { 
                audioOutputManager.HandleAudioPacket(AudioPacket.ReadFromMessage(msg));
            };
            client.OnClientConnected += VoiceClientConnected;

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
            //if (isPaused)
            //    audioOutputManager.ClearAudio();
        }

        private void VoiceClientConnected(object sender, EventArgs e)
        {
            audioClientConnected = true;
            capi.Logger.Debug("[RPVoiceChat - Client] Voice client connected");

            // Set up game events
            capi.Event.RegisterGameTickListener(OnGameTick, 1);
        }

        private void OnGameTick(float dt)
        {
            if(micManager == null || audioOutputManager == null)
                return;

            micManager.isGamePaused = capi.IsGamePaused;
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
    }
}
