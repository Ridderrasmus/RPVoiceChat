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
        private readonly VoicePacketWorker<Packet_CustomPacket> incoming;

        private ICoreClientAPI capi;

        public NativeNetworkClient(ICoreClientAPI api) : base(api)
        {
            capi = api;
            channel = api.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(HandleAudioPacket);
            incoming = new VoicePacketWorker<Packet_CustomPacket>(
                packet => ((NetworkChannel)channel).OnPacket(packet),
                error => capi.Logger.Warning("[RPVoiceChat] Native voice receive failed: " + error.Message));
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


            if (channel is not NetworkChannel) return false;

            var expectedChannelId = (int)channelIdField.GetValue(channel);
            if (channelId != expectedChannelId) return false;
            // The game's network thread also delivers world updates. Only hand off here;
            // protobuf decoding, source creation and playback must not hold it up.
            incoming.Enqueue(customPacket);
            return true;
        }

        private void HandleAudioPacket(AudioPacket packet)
        {
            OnAudioReceived?.Invoke(packet);
        }

        public void Dispose()
        {
            SystemNetworkProcessPatch.OnProcessInBackground -= ProcessInBackground;
            incoming.Dispose();
        }
    }
}
