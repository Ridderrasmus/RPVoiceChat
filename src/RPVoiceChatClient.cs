using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace rpvoicechat
{
    public class RPVoiceChatClient : RPVoiceChatMod
    {
        MicrophoneManager micManager;
        AudioOutputManager audioOutputManager;

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            base.StartClientSide(capi);

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

            // Set up game events
            capi.Event.RegisterGameTickListener(OnGameTick, 20);
            capi.Event.LeftWorld += OnPlayerLeaving;
            capi.Event.PauseResume += OnPauseResume;

            // Initialize gui
            MainConfig configGui = new MainConfig(capi, micManager);

            // Set up keybinds
            capi.Input.RegisterHotKey("voicechatMenu", "Voice: Config menu", GlKeys.P, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatCycleDevice", "Voice: Switch input device", GlKeys.N, HotkeyType.GUIOrOtherControls, true);
            capi.Input.RegisterHotKey("voicechatPTT", "Voice: Push to talk", GlKeys.CapsLock, HotkeyType.GUIOrOtherControls);

            // Set up keybind event handlers
            capi.Input.SetHotKeyHandler("voicechatMenu", (t1) => 
            {
                configGui.Toggle();
                return true;
            });
            
            capi.Input.SetHotKeyHandler("voicechatCycleDevice", (t1) =>
            {
                var mic = micManager.CycleInputDevice();
                capi.ShowChatMessage("Voice: Input device set to " + mic.ProductName);
                return true;
            });

        }

        private void OnPauseResume(bool isPaused)
        {
            if (isPaused)
                audioOutputManager.ClearAllAudio();
        }

        private void VoiceClientConnected(object sender, EventArgs e)
        {
            capi.Logger.Debug("[RPVoiceChat - Client] Voice client connected");
            

            micManager.OnBufferRecorded += (buffer, voiceLevel) =>
            {

                AudioPacket packet = new AudioPacket()
                {
                    PlayerId = capi.World.Player.PlayerUID,
                    AudioData = buffer,
                    voiceLevel = voiceLevel
                };
                client.SendAudioToServer(packet);
            };
        }

        private void OnGameTick(float dt)
        {

            if(micManager == null)
                return;

            micManager.isGamePaused = capi.IsGamePaused;
            micManager.playersNearby = capi.World.GetPlayersAround(capi.World.Player.Entity.Pos.XYZ, (int)VoiceLevel.Shouting + 10, (int)VoiceLevel.Shouting);

#if !DEBUG
            if (micManager.playersNearby.Count() <= 1)
                return;
#endif

            foreach (var player in micManager.playersNearby)
            {
#if !DEBUG
                if (player.PlayerUID == capi.World.Player.PlayerUID)
                    continue;
#endif
                BlockSelection blockSelection = new BlockSelection();
                EntitySelection entitySelection = new EntitySelection();
                capi.World.RayTraceForSelection(player.Entity.Pos.XYZ, capi.World.Player.Entity.Pos.XYZ, ref blockSelection, ref entitySelection);

                audioOutputManager.UpdatePlayerSource(player.PlayerUID, player.Entity.Pos.XYZ);
                audioOutputManager.SetPlayerMuffled(player.PlayerUID, blockSelection != null);

            }

            audioOutputManager.UpdateAudio();
        }

        private void OnPlayerLeaving()
        {
            client.Close();
        }

        private void OnConnectionInfo(ConnectionInfo packet)
        {
            if (packet == null) return;
            client.ConnectToServer(packet.Address, packet.Port);
        }
    }
}
