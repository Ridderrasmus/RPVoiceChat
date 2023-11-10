using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace RPVoiceChat.Networking
{
    public class NativeNetworkClient : NativeNetworkBase, INetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;
        private IClientNetworkChannel channel;
        private IClientNetworkChannel singleplayerChannel;

        public NativeNetworkClient(ICoreClientAPI api) : base(api)
        {
            channel = api.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(HandleAudioPacket);
            if (api.IsSinglePlayer)
                singleplayerChannel = api.Network.RegisterChannel(SPChannelName).RegisterMessageType<AudioPacket>();
            SystemNetworkProcessPatch.OnProcessInBackground += ProcessInBackground;
        }

        public bool SendAudioToServer(AudioPacket packet)
        {
            var channel = singleplayerChannel ?? this.channel;
            channel.SendPacket(packet);
            return true;
        }

        private static FieldInfo channelIdField = AccessTools.Field(typeof(NetworkChannel), "channelId");

        private bool ProcessInBackground(int channelId, Packet_CustomPacket customPacket)
        {
            if (channel is not NetworkChannel nativeChannel) return false;

            var expectedChannelId = (int)channelIdField.GetValue(channel);
            if (channelId != expectedChannelId) return false;

            nativeChannel.OnPacket(customPacket);
            return true;
        }

        private void HandleAudioPacket(AudioPacket packet)
        {
            OnAudioReceived?.Invoke(packet);
        }

        public void Dispose()
        {
            SystemNetworkProcessPatch.OnProcessInBackground -= ProcessInBackground;
        }
    }
}
