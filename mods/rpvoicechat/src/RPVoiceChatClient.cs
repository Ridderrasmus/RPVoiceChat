using System;
using System.Collections.Generic;
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
            capi = api;
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

        }

        private void OnPauseResume(bool isPaused)
        {
            if (isPaused)
                audioOutputManager.ClearAudio();
        }

        private void VoiceClientConnected(object sender, EventArgs e)
        {
            capi.Logger.Debug("[RPVoiceChat - Client] Voice client connected");

            // Set up game events
            capi.Event.RegisterGameTickListener(OnGameTick, 20);
            capi.Event.LeftWorld += OnPlayerLeaving;
            capi.Event.PauseResume += OnPauseResume;

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

            if(micManager == null || audioOutputManager == null)
                return;

            micManager.isGamePaused = capi.IsGamePaused;

            audioOutputManager.SetListenerPosition(capi.World.Player.Entity.Pos);

            bool playersNearby = false;
            foreach (var player in capi.World.AllOnlinePlayers)
            {

                // Ignore self
                if (player.PlayerUID == capi.World.Player.PlayerUID)
                    continue;

                // Update player audio source
                Task.Run(() => audioOutputManager.UpdatePlayerSource(player));
                
                // Player is not loaded in clientside
                if (player.Entity == null)
                    continue;

                // If player is nearby then add to list of players nearby
                if (player.Entity.Pos.SquareDistanceTo(capi.World.Player.Entity.Pos) < ((int)VoiceLevel.SquareShouting + 100))
                    playersNearby = true;
            }

            // Determine if players are nearby which determines if we should be transmitting audio

            micManager.playersNearby = playersNearby;

            audioOutputManager.PlayAudio();
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
