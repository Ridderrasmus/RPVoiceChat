using System;
using Vintagestory.API.Client;

namespace RPVoiceChat.Networking
{
    public class NativeNetworkClient : NativeNetworkBase, INetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;
        private IClientNetworkChannel channel;

        public NativeNetworkClient(ICoreClientAPI api) : base(api)
        {
            channel = api.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(HandleAudioPacket);
        }

        public bool SendAudioToServer(AudioPacket packet)
        {
            channel.SendPacket(packet);
            return true;
        }

        private void HandleAudioPacket(AudioPacket packet)
        {
            OnAudioReceived?.Invoke(packet);
        }
    }
}
