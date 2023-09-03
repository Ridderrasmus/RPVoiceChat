using System;

namespace RPVoiceChat.Networking
{
    public interface INetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;

        public string GetTransportID();
        public void SendAudioToServer(AudioPacket packet);
    }
}
