using System;
using System.Threading.Tasks;
using Vintagestory.API.Client;

namespace rpvoicechat.Networking
{
    public class RPVoiceChatNativeNetworkClient : RPVoiceChatNativeNetwork
    {
        public event Action<AudioPacket> OnAudioReceived;
        private IClientNetworkChannel channel;

        public RPVoiceChatNativeNetworkClient(ICoreClientAPI api) : base(api)
        {
            channel = api.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(HandleAudioPacket);
        }

        public async void SendAudioToServer(AudioPacket packet)
        {
            await Task.Run(() =>
            {
                channel.SendPacket(packet);
            });
        }

        private void HandleAudioPacket(AudioPacket packet)
        {
            OnAudioReceived?.Invoke(packet);
        }
    }
}
