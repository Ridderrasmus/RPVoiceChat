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
        public event Action<PlayerAudioPacket> OnClientAudioPacketReceived;

        private ICoreClientAPI clientApi;
        private WaveInEvent waveIn;
        public bool isInitialized = false;

        public RPVoiceChatSocketClient(ICoreClientAPI clientApi) 
        {
            this.IsServer = false;
            this.clientApi = clientApi;

            port = int.Parse(clientApi.World.Config.GetString("rpvoicechat:port", "52525"));

            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Bind the socket to a local port
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 0); // 0 lets the OS pick a free port
            clientSocket.Bind(localEndPoint);

            

            Task.Run(() => StartAsync());
        }

        public async Task StartAsync()
        {
            StartListening();
        }

        public void StartListening()
        {
            Task.Run(() =>
            {
                byte[] buffer = new byte[bufferSize];
                while (RemoteEndPoint == null) { }
                while (true)
                {
                    EndPoint remoteEP = RemoteEndPoint;
                    int receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);

                    byte[] receivedData = new byte[receivedBytes];
                    Array.Copy(buffer, 0, receivedData, 0, receivedBytes);

                    PlayerAudioPacket packet = DeserializePacket(receivedData);

                    // Invoke the event
                    OnClientAudioPacketReceived?.Invoke(packet);
                }
            });
        }

        public void ConnectToServer(string serverAddress)
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse(serverAddress), port);
        }

        public void SendAudioPacket(PlayerAudioPacket packet)
        {
            byte[] buffer = SerializePacket(packet);
            clientSocket.SendTo(buffer, RemoteEndPoint);
        }

        private void OnAudioRecorded(object sender, WaveInEventArgs e)
        {
            if (RemoteEndPoint == null) return;

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
