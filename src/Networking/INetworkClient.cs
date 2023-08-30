using rpvoicechat;
using System;

namespace RPVoiceChat
{
    public interface INetworkClient : INetworkCommon
    {
        public void SendAudioToServer(AudioPacket packet);

        public event Action<AudioPacket> OnAudioReceived;
    }
}
