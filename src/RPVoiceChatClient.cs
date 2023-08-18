using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading.Tasks;
using Lidgren.Network;
using rpvoicechat.Networking;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace rpvoicechat
{
    public class RPVoiceChatClient : RPVoiceChatMod
    {
        private MicrophoneManager micManager;
        private AudioOutputManager audioOutputManager;
        //private RPVoiceChatSocketClient client;
        private RPVoiceChatNativeNetworkClient client;

        protected ICoreClientAPI capi;

        private MainConfig configGui;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            // Init microphone and audio output manager
            micManager = new MicrophoneManager(capi);
            audioOutputManager = new AudioOutputManager(capi);
            
            // Init voice chat client
            client = new RPVoiceChatNativeNetworkClient(api);

            // Add voice chat client event handlers
            //client.OnMessageReceived += OnMessageReceived;
            //client.OnClientConnected += VoiceClientConnected;
            //client.OnClientDisconnected += VoiceClientDisconnected;

            client.OnAudioReceived += OnAudioReceived;

            // Initialize gui
            configGui = new MainConfig(capi, micManager, audioOutputManager);
            api.Gui.RegisterDialog(new HudIcon(capi, micManager));

            // Set up keybinds
            capi.Input.RegisterHotKey("voicechatMenu", "RPVoice: Config menu", GlKeys.P, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatVoiceLevel", "RPVoice: Change speech volume", GlKeys.Tab, HotkeyType.GUIOrOtherControls, false, false, true);
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
                capi.ShowChatMessage("RPVoice: Speech volume set to " + level.ToString());
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

                AudioPacket packet = new AudioPacket()
                {
                    PlayerId = capi.World.Player.PlayerUID,
                    AudioData = buffer,
                    Length = length,
                    VoiceLevel = voiceLevel
                };
                client.SendAudioToServer(packet);
            };
        }

        private void OnAudioReceived(AudioPacket obj)
        {
            audioOutputManager.HandleAudioPacket(obj);
        }

        public override void Dispose()
        {
            micManager?.Dispose();
            //client?.Dispose();
            configGui.Dispose();
            //client = null;
        }
    }
}
