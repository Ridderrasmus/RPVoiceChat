
using NAudio.Wave;
using rpvoicechat.Client.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace rpvoicechat
{
    public class RPVoiceChatClient : RPVoiceChatCommon
    {
        IClientNetworkChannel clientChannel;
        ICoreClientAPI clientApi;
        HudElement hudElement;
        RPAudioInputManager audioInputManager;
        RPAudioOutputManager audioOutputManager;
        RPVoiceChatSocketClient socketClient;
        VoiceLevel voiceLevel = VoiceLevel.Normal;



        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            clientApi = api;

            // Register game tick listener
            clientApi.Event.RegisterGameTickListener(OnGameTick, 20);
            clientApi.Event.PlayerLeave += OnPlayerLeave;

            // Register clientside connection to the network channel
            clientChannel = clientApi.Network.GetChannel("rpvoicechat")
                .SetMessageHandler<ConnectionPacket>(OnHandshakeRecieved);

            // Custom keybindings
            GlobalKeyboardHook keyboardHook = new GlobalKeyboardHook();
            keyboardHook.KeyDown += OnKeyDown;
            keyboardHook.KeyUp += OnKeyUp;

            // Add a new keybinding
            clientApi.Input.RegisterHotKey("voiceVolume", "Voice: Change voice volume", GlKeys.N, HotkeyType.GUIOrOtherControls);
            clientApi.Input.RegisterHotKey("changeMic", "Voice: Cycle microphone", GlKeys.N, HotkeyType.GUIOrOtherControls, true);
            clientApi.Input.RegisterHotKey("changeActivation", "Voice: Cycle activation mode", GlKeys.N, HotkeyType.GUIOrOtherControls, false, true);
            clientApi.Input.RegisterHotKey("toggleMute", "Voice: Toggle mute", GlKeys.M, HotkeyType.GUIOrOtherControls, false, false, true);

            // Add hotkey handler
            clientApi.Input.SetHotKeyHandler("voiceVolume", ChangeVoiceVolume);
            clientApi.Input.SetHotKeyHandler("changeMic", ChangeMic);
            clientApi.Input.SetHotKeyHandler("changeActivation", ChangeActivation);
            clientApi.Input.SetHotKeyHandler("toggleMute", ToggleMute);

            // Initialize the socket client
            socketClient = new RPVoiceChatSocketClient(clientApi);
            socketClient.OnClientAudioPacketReceived += OnAudioReceived;

            // Initialize the audio output manager
            audioOutputManager = new RPAudioOutputManager(clientApi);

            // Initialize the hud element


            clientApi.Logger.Debug("[RPVoiceChat] Client started");


        }

        private void OnPlayerLeave(IClientPlayer byPlayer)
        {
            if (byPlayer != clientApi.World.Player) return;

            socketClient.Close();
        }

        private void OnHandshakeRecieved(ConnectionPacket packet)
        {
            clientChannel.SendPacket(new ConnectionPacket() { packetIp = GetPublicIp(), packetPort = socketClient.localPort });
            socketClient.ConnectToServer(packet.packetIp);

            // Initialize the audio input manager
            audioInputManager = new RPAudioInputManager(socketClient, clientApi);

            clientApi.Logger.Debug("[RPVoiceChat] Handshake recieved");
        }

        private bool ChangeActivation(KeyCombination t1)
        {
            if (audioInputManager.CurrentActivationMode == RPAudioInputManager.ActivationMode.VoiceActivation)
            {
                audioInputManager.SetActivationMode(RPAudioInputManager.ActivationMode.PushToTalk);
                clientApi.ShowChatMessage("Activation mode: Push to talk");
            }
            else
            {
                audioInputManager.SetActivationMode(RPAudioInputManager.ActivationMode.VoiceActivation);
                clientApi.ShowChatMessage("Activation mode: Voice activation");
            }

            return true;
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.CapsLock)
            {
                audioInputManager.TogglePushToTalk(false);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.CapsLock)
            {
                audioInputManager.TogglePushToTalk(true);
            }
        }

        private bool ToggleMute(KeyCombination t1)
        {
            audioInputManager.ToggleMute();
            if (audioInputManager.isMuted) clientApi.ShowChatMessage("Microphone muted");
            else clientApi.ShowChatMessage("Microphone unmuted");
            return true;
        }

        // Function to change the voice volume
        private bool ChangeVoiceVolume(KeyCombination t1)
        {
            VoiceLevel voiceLevel = audioInputManager.CycleVoiceLevel();
            clientApi.ShowChatMessage("Voice volume set to " + Enum.GetName(typeof(VoiceLevel), voiceLevel).ToLower());

            return true;
        }

        private bool ChangeMic(KeyCombination t1)
        {
            if (audioInputManager == null) return true;
            if (!audioInputManager.canSwitchDevice) return true;

            audioInputManager.canSwitchDevice = false;
            WaveInCapabilities deviceName = audioInputManager.CycleInputDevice();
            clientApi.ShowChatMessage("Microphone set to " + deviceName.ProductName);

            audioInputManager.canSwitchDevice = true;
            return true;
        }

        private void OnGameTick(float dt)
        {
            if (audioInputManager == null) return;

            audioInputManager.playersNearby = GetPlayersInRange((int)voiceLevel);
            ConcurrentDictionary<string, PlayerAudioSource> audioSources = new ConcurrentDictionary<string, PlayerAudioSource>();
            foreach (IPlayer player in GetPlayersInRange((int)VoiceLevel.Shout))
            {
                BlockSelection blockSel = new BlockSelection();
                EntitySelection entitySel = new EntitySelection();
                clientApi.World.RayTraceForSelection(player.Entity.Pos.XYZ, clientApi.World.Player.Entity.Pos.XYZ, ref blockSel, ref entitySel);

                audioOutputManager.SetPlayerMuffled(player.PlayerUID, !(blockSel == null));
                audioOutputManager.UpdatePlayerSource(player.PlayerUID, player.Entity.Pos.XYZ);
            }
            

            // If the player is recording audio show the audio icon
            if (audioInputManager.isRecording)
            {
                // Get the player characters voice type (Trumpet, Tuba, etc.)
                //string voiceSound = clientApi.World.Player.Entity.talkUtil.soundName.GetName();
                //clientApi.ShowChatMessage("Voice sound is: " + voiceSound);

                // Do talking animation
                //clientApi.World.Player.Entity.AnimManager.StartAnimation();
                
            }

        }

        // When client recieves audio data from the server
        public void OnAudioReceived(PlayerAudioPacket packet)
        {
            audioOutputManager.HandleAudioPacket(packet);
        }




        private IPlayer[] GetPlayersInRange(int distance)
        {
            return clientApi.World.Player.Entity.World.GetPlayersAround(clientApi.World.Player.Entity.Pos.XYZ, distance, distance);
        }
    }
}
