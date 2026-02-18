using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Networking;
using Vintagestory.API.Client;

namespace RPVoiceChat.Client
{
    public class VoiceGroupClientManager
    {
        private readonly ICoreClientAPI capi;
        private readonly IClientNetworkChannel voiceGroupChannel;
        private readonly Dictionary<string, VoiceGroupStateEntry> groupsByName = new Dictionary<string, VoiceGroupStateEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> groupByPlayerUid = new Dictionary<string, string>();

        public VoiceGroupClientManager(ICoreClientAPI api)
        {
            capi = api;
            voiceGroupChannel = api.Network
                .RegisterChannel("RPVoiceGroups")
                .RegisterMessageType<VoiceGroupStatePacket>()
                .SetMessageHandler<VoiceGroupStatePacket>(OnVoiceGroupsUpdated);
        }

        public bool IsPlayerInGroup(string playerUid)
        {
            return groupByPlayerUid.ContainsKey(playerUid);
        }

        public bool TryGetPlayerGroup(string playerUid, out string groupName)
        {
            return groupByPlayerUid.TryGetValue(playerUid, out groupName);
        }

        public bool ArePlayersInSameGroup(string firstPlayerUid, string secondPlayerUid)
        {
            return groupByPlayerUid.TryGetValue(firstPlayerUid, out var firstGroup)
                && groupByPlayerUid.TryGetValue(secondPlayerUid, out var secondGroup)
                && string.Equals(firstGroup, secondGroup, StringComparison.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<string> GetMembersOfGroup(string groupName)
        {
            if (!groupsByName.TryGetValue(groupName, out var group))
            {
                return Array.Empty<string>();
            }

            return group.Members;
        }

        private void OnVoiceGroupsUpdated(VoiceGroupStatePacket packet)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                groupsByName.Clear();
                groupByPlayerUid.Clear();

                foreach (var group in packet.Groups)
                {
                    if (string.IsNullOrWhiteSpace(group?.Name))
                    {
                        continue;
                    }

                    groupsByName[group.Name] = group;
                    foreach (var member in group.Members ?? Enumerable.Empty<string>())
                    {
                        if (!groupByPlayerUid.ContainsKey(member))
                        {
                            groupByPlayerUid[member] = group.Name;
                        }
                    }
                }

                capi.Event.PushEvent("rpvoicechat:voiceGroupUpdate");
            }, "rpvoicechat:VoiceGroupUpdate");
        }
    }
}
