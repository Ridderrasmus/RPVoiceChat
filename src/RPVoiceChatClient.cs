﻿using System;
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

            // Set up game events
            capi.Event.RegisterGameTickListener(OnGameTick, 20);
            capi.Event.LeftWorld += OnPlayerLeaving;
            capi.Event.PauseResume += OnPauseResume;

            // Initialize gui
            MainConfig configGui = new MainConfig(capi, micManager, audioOutputManager);
            api.Gui.RegisterDialog(new HudIcon(capi, micManager));

            // Set up keybinds
            capi.Input.RegisterHotKey("voicechatMenu", "RPVoice: Config menu", GlKeys.P, HotkeyType.GUIOrOtherControls);
            capi.Input.RegisterHotKey("voicechatVoiceLevel", "RPVoice: Change voice volume", GlKeys.Tab, HotkeyType.GUIOrOtherControls, false, false, true);
            capi.Input.RegisterHotKey("voicechatPTT", "RPVoice: Push to talk", GlKeys.CapsLock, HotkeyType.GUIOrOtherControls);

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

        }

        private void OnPauseResume(bool isPaused)
        {
            if (isPaused)
                audioOutputManager.ClearAudio();
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

            if(micManager == null || audioOutputManager == null)
                return;

            micManager.isGamePaused = capi.IsGamePaused;

            audioOutputManager.SetListenerPosition(capi.World.Player.Entity.Pos);

            bool playerNearby = false;

            foreach (var player in capi.World.AllOnlinePlayers)
            {

                // Ignore self
                if (player.PlayerUID == capi.World.Player.PlayerUID)
                    continue;

                // Update player audio source
                Task.Run(() => audioOutputManager.UpdatePlayerSource(player));

                // Ignore players too far away
                if (player.Entity.Pos.SquareDistanceTo(capi.World.Player.Entity.Pos) > ((int)VoiceLevel.SqrShouting + 100/*because 10^2=100*/))
                    continue;

                playerNearby = true;
            }

            // Determine if players are nearby which determines if we should be transmitting audio
            micManager.playersNearby = playerNearby;

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
