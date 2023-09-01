using rpvoicechat;
using System;
using System.Threading.Tasks;
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
