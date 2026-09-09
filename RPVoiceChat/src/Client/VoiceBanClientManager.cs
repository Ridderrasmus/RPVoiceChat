using System;
using System.Collections.Generic;
using RPVoiceChat.Networking;
using Vintagestory.API.Client;

namespace RPVoiceChat.Client
{
    public class VoiceBanClientManager
    {
        private ICoreClientAPI capi;
        private HashSet<string> bannedPlayers = new HashSet<string>();
        private IClientNetworkChannel voiceBanChannel;

        public VoiceBanClientManager(ICoreClientAPI api)
        {
            capi = api;
            voiceBanChannel = api.Network
                .RegisterChannel("RPVoiceBan")
                .RegisterMessageType<VoiceBanStatusPacket>()
                .SetMessageHandler<VoiceBanStatusPacket>(OnVoiceBanStatusReceived);
        }

        public bool IsPlayerBanned(string playerUID)
        {
            return bannedPlayers.Contains(playerUID);
        }

        private void OnVoiceBanStatusReceived(VoiceBanStatusPacket packet)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (packet.IsBanned)
                {
                    bannedPlayers.Add(packet.PlayerId);
                }
                else
                {
                    bannedPlayers.Remove(packet.PlayerId);
                }

                // Notify the UI to update the display
                capi.Event.PushEvent("rpvoicechat:voiceBanUpdate");
            }, "rpvoicechat:VoiceBanUpdate");
        }
    }
}

