using HarmonyLib;
using RPVoiceChat.Utils;
using System;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace RPVoiceChat.Networking
{
    public class NativeNetworkClient : NativeNetworkBase, INetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;
        private IClientNetworkChannel channel;

        public NativeNetworkClient(ICoreClientAPI api) : base(api)
        {
            channel = api.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(HandleAudioPacket);
            SystemNetworkProcessPatch.OnProcessInBackground += ProcessInBackground;
        }

        public bool SendAudioToServer(AudioPacket packet)
        {
            channel.SendPacket(packet);
            return true;
        }

        private bool ProcessInBackground(int channelId, Packet_CustomPacket customPacket)
        {
            if (channel is not NetworkChannel nativeChannel) return false;

            var expectedChannelId = (int)AccessTools.Field(typeof(NetworkChannel), "channelId").GetValue(channel);
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
