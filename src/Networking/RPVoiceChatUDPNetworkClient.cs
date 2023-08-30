using ProtoBuf;
using rpvoicechat;
using System;
using System.IO;
using System.Net;

namespace RPVoiceChat
{
    public class RPVoiceChatUDPNetworkClient : RPVoiceChatUDPNetwork, INetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;

        public RPVoiceChatUDPNetworkClient() : base()
        {
            OnMessageReceived += MessageReceived;

            StartListening(52525);
        }

        public RPVoiceChatUDPNetworkClient(int port) : base(port)
        {
            OnMessageReceived += MessageReceived;

            StartListening(port);
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            throw new NotImplementedException();
        }

        private void MessageReceived(byte[] msg)
        {
            AudioPacket packet = Serializer.Deserialize<AudioPacket>(new MemoryStream(msg));
            OnAudioReceived?.Invoke(packet);
        }
    }
}
