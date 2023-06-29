using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using Vintagestory.API.Client;
using NAudio.Wave;
using Vintagestory.API.Common;
using System;

namespace rpvoicechat
{
    public class RPVoiceChatSocketClient : RPVoiceChatSocketCommon
    {
        private ICoreClientAPI clientApi;
        private IPEndPoint serverEndPoint = null;
        private WaveInEvent waveIn;
        public bool isInitialized = false;

        public RPVoiceChatSocketClient(ICoreClientAPI clientApi) 
        {
            this.IsServer = false;
            this.clientApi = clientApi;

            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Bind the socket to a local port
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 0); // 0 lets the OS pick a free port
            clientSocket.Bind(localEndPoint);

            Task.Run(() => StartAsync());
        }

        public async Task StartAsync()
        {
            StartListening(this, clientSocket);
        }

        public void ConnectToServer(string serverAddress)
        {
            serverEndPoint = new IPEndPoint(IPAddress.Parse(serverAddress), port);
        }

        public void SendAudioPacket(PlayerAudioPacket packet)
        {
            byte[] buffer = SerializePacket(packet);
            clientSocket.SendTo(buffer, serverEndPoint);
        }

        private void OnAudioRecorded(object sender, WaveInEventArgs e)
        {
            if (serverEndPoint == null) return;

            byte[] buffer = e.Buffer;

            PlayerAudioPacket packet = new PlayerAudioPacket();
            packet.playerUid = clientApi.World.Player.PlayerUID;
            packet.audioData = buffer;

            SendAudioPacket(packet);
        }

        public void Close()
        {
            // Release resources
            waveIn.StopRecording();
            waveIn.Dispose();
            clientSocket.Close();
        }

    }
}
