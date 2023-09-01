using System;

namespace RPVoiceChat.Networking
{
    public interface INetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;

        public void SendAudioToServer(AudioPacket packet);
    }
}
