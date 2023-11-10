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

        private static FieldInfo channelIdField = AccessTools.Field(typeof(NetworkChannel), "channelId");

        private bool ProcessInBackground(int channelId, Packet_CustomPacket customPacket, IServerPlayer sender)
        {
            if (channel is not NetworkChannel nativeChannel) return false;

            var expectedChannelId = (int)channelIdField.GetValue(channel);
            if (channelId != expectedChannelId) return false;

            // Since we don't remove custom packets from original queue, all of them will be duplicated.
            // In case of AudioPackets this isn't an issue but if this behavior is undesired - change SetMessageHandler
            // To an empty function and copy handler delegate code from Vintagestory.Server.NetworkChannel.SetMessageHandler
            // - Dmitry221060, 10.11.2023
            nativeChannel.OnPacket(customPacket, sender);
            return true;
        }

        private void ReceivedAudioPacketFromClient(IServerPlayer player, AudioPacket packet)
        {
            AudioPacketReceived?.Invoke(packet);
        }

        public void Dispose()
        {
            networkProcess?.Dispose();
        }
    }
}
