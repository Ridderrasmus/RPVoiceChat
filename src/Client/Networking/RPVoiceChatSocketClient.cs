using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using System;

namespace rpvoicechat
{
    public class RPVoiceChatSocketClient : RPVoiceChatSocketCommon
    {
        public event Action<PlayerAudioPacket> OnClientAudioPacketReceived;

        private ICoreClientAPI clientApi;
        public bool isInitialized = false;
        public int localPort = 0;

        public RPVoiceChatSocketClient(ICoreClientAPI clientApi) 
        {
            this.IsServer = false;
            this.clientApi = clientApi;

            port = int.Parse(clientApi.World.Config.GetString("rpvoicechat:port", "52525"));

            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Bind the socket to a local port
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 0); // 0 lets the OS pick a free port
            clientSocket.Bind(localEndPoint);
            localPort = ((IPEndPoint)clientSocket.LocalEndPoint).Port;

            

            Task.Run(() => StartListening());
        }

        public void StartListening()
        {
            byte[] buffer = new byte[bufferSize];
            while (RemoteEndPoint == null) { }
            while (true)
            {
                EndPoint remoteEP = RemoteEndPoint;
                int receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);


                byte[] receivedData = new byte[receivedBytes];
                Array.Copy(buffer, 0, receivedData, 0, receivedBytes);

                // TODO: Move deserialization to the invoked method

                try
                {
                    PlayerAudioPacket packet = DeserializePacket(receivedData);
                    //packet.audioData = AudioUtils.DecodeAudio(packet.audioData);
                    
                    // Invoke the event
                    OnClientAudioPacketReceived?.Invoke(packet);
                }
                catch (Exception e)
                {
                    //clientApi.Logger.Error(e.Message);
                    continue;
                }

            }
        }

        public void ConnectToServer(string serverAddress)
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse(serverAddress), port);
        }

        public void SendAudioPacket(PlayerAudioPacket packet)
        {
            //packet.audioData = AudioUtils.EncodeAudio(packet.audioData);
            byte[] buffer = SerializePacket(packet);
            if (buffer.Length > bufferSize)
            {
                throw new Exception("Packet size is too large: " + buffer.Length);
            }
            clientSocket.SendTo(buffer, RemoteEndPoint);
        }


        public void Close()
        {
            // Release resources
            //ca.StopRecording();
            //waveIn.Dispose();
            clientSocket.Close();
        }

    }
}
