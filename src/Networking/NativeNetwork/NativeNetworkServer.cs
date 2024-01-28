using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace RPVoiceChat.Networking
{
    public class NativeNetworkServer : NativeNetworkBase, INetworkServer
    {
        public event Action<AudioPacket> AudioPacketReceived;
        private ICoreServerAPI api;
        private IServerNetworkChannel channel;
        private ServerSystemNetworkProcess networkProcess;

        public NativeNetworkServer(ICoreServerAPI sapi) : base(sapi)
        {
            api = sapi;
            channel = api.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(ReceivedAudioPacketFromClient);
            if (api.Server.IsDedicated == false)
                api.Network.RegisterChannel(SPChannelName)
                    .RegisterMessageType<AudioPacket>()
                    .SetMessageHandler<AudioPacket>(ReceivedAudioPacketFromClient);

            NetworkAPIPatch.OnHandleCustomPacket += ShouldProcessInBackground;
        }

        public void Launch()
        {
            networkProcess = new ServerSystemNetworkProcess(api);
            networkProcess.OnProcessInBackground += ProcessInBackground;
            networkProcess.Launch();
        }

        public ConnectionInfo GetConnectionInfo()
        {
            var connectionInfo = new ConnectionInfo();
            return connectionInfo;
        }

        public bool SendPacket(NetworkPacket packet, string playerId)
        {
            var player = api.World.PlayerByUid(playerId) as IServerPlayer;
            channel.SendPacket(packet as AudioPacket, player);
            return true;
        }

        private bool ProcessInBackground(int channelId, Packet_CustomPacket customPacket, IServerPlayer sender)
        {
            if (ShouldProcessInBackground(channelId) == false) return false;

            ((NetworkChannel)channel).OnPacket(customPacket, sender);
            return true;
        }

        private static FieldInfo channelIdField = AccessTools.Field(typeof(NetworkChannel), "channelId");

        private bool ShouldProcessInBackground(int channelId)
        {
            if (channel is not NetworkChannel) return false;
            var expectedChannelId = (int)channelIdField.GetValue(channel);
            return channelId == expectedChannelId;
        }

        private void ReceivedAudioPacketFromClient(IServerPlayer player, AudioPacket packet)
        {
            AudioPacketReceived?.Invoke(packet);
        }

        public void Dispose()
        {
            NetworkAPIPatch.OnHandleCustomPacket -= ShouldProcessInBackground;
            networkProcess?.Dispose();
        }
    }
}
