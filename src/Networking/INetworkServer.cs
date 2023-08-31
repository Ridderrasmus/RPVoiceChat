using rpvoicechat;
using System;
using Vintagestory.API.Server;

namespace RPVoiceChat.Networking
{
    public interface INetworkServer
    {
        public event Action<IServerPlayer, AudioPacket> OnReceivedPacket;

        public void SendPacket(AudioPacket packet, IServerPlayer player);
    }
}
