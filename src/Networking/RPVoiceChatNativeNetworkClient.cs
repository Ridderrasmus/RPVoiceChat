
using System;
using System.Threading.Tasks;
using Vintagestory.API.Client;

namespace rpvoicechat.src.Networking
{
    public class RPVoiceChatNativeNetworkClient : RPVoiceChatNativeNetwork
    {
        private ICoreClientAPI api;

        public Action<AudioPacket> OnAudioReceived;
        private IClientNetworkChannel channel;

        public RPVoiceChatNativeNetworkClient(ICoreClientAPI api) : base(api)
        {
            this.api = api;
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
