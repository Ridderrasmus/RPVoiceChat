using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Networking;
using Vintagestory.API.Client;

namespace RPVoiceChat.Client
{
    public class VoiceGroupClientManager : IDisposable
    {
        private readonly ICoreClientAPI capi;
        private readonly IClientNetworkChannel voiceGroupChannel;
        private readonly Dictionary<string, VoiceGroupStateEntry> groupsByName = new Dictionary<string, VoiceGroupStateEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> groupByPlayerUid = new Dictionary<string, string>();
        private readonly long timeoutTick;
        private long requestStarted;
        private bool disposed;
        public VoiceGroupUiStatePacket State { get; private set; } = new();
        public bool IsReady { get; private set; }
        public bool ActionPending { get; private set; }
        public event Action Changed;
        public event Action<VoiceGroupActionResultPacket> ActionResult;
        public VoiceGroupStateEntry OwnGroup => State.Groups.FirstOrDefault(group => group.Members.Contains(capi.World.Player?.PlayerUID));

        public VoiceGroupClientManager(ICoreClientAPI api)
        {
            capi = api;
            voiceGroupChannel = api.Network
                .RegisterChannel("RPVoiceGroups")
                .RegisterMessageType<VoiceGroupStatePacket>()
                .RegisterMessageType<VoiceGroupActionPacket>()
                .RegisterMessageType<VoiceGroupUiStatePacket>()
                .RegisterMessageType<VoiceGroupActionResultPacket>()
                .SetMessageHandler<VoiceGroupStatePacket>(OnVoiceGroupsUpdated)
                .SetMessageHandler<VoiceGroupUiStatePacket>(packet => capi.Event.EnqueueMainThreadTask(() =>
                {
                    if (disposed || packet == null) return;
                    var previousInvitations = State.Invitations;
                    State = packet;
                    IsReady = true;
                    ApplyGroups(packet.Groups);
                    Changed?.Invoke();
                    if (packet.Invitations.Except(previousInvitations).Any())
                        capi.ShowChatMessage(RPVoiceChat.Util.UIUtils.I18n("Gui.VoiceGroups.InvitationNotice"));
                }, "rpvoicechat:groupUiState"))
                .SetMessageHandler<VoiceGroupActionResultPacket>(packet => capi.Event.EnqueueMainThreadTask(() =>
                {
                    if (disposed || packet == null) return;
                    ActionPending = false;
                    ActionResult?.Invoke(packet);
                }, "rpvoicechat:groupActionResult"));
            timeoutTick = capi.Event.RegisterGameTickListener(_ =>
            {
                if (!ActionPending || Environment.TickCount64 - requestStarted < 8000) return;
                ActionPending = false;
                ActionResult?.Invoke(new VoiceGroupActionResultPacket
                {
                    Message = RPVoiceChat.Util.UIUtils.I18n("Gui.VoiceGroups.RequestTimeout")
                });
                Refresh();
            }, 500);
        }

        public void Refresh() => voiceGroupChannel.SendPacket(new VoiceGroupActionPacket { Action = VoiceGroupAction.Refresh });

        public bool Send(VoiceGroupAction action, string groupName = null, string targetUid = null, bool value = false)
        {
            if (disposed || ActionPending || !IsReady) return false;
            ActionPending = true;
            requestStarted = Environment.TickCount64;
            voiceGroupChannel.SendPacket(new VoiceGroupActionPacket
            {
                Action = action, GroupName = groupName, TargetPlayerUid = targetUid, Value = value
            });
            return true;
        }

        public string PlayerName(string uid) => State.Players.FirstOrDefault(p => p.Uid == uid)?.Name
            ?? capi.World.PlayerByUid(uid)?.PlayerName ?? uid;

        public bool IsOnline(string uid) => State.Players.Any(p => p.Uid == uid && p.Online);

        public void Dispose()
        {
            disposed = true;
            capi.Event.UnregisterGameTickListener(timeoutTick);
            Changed = null;
            ActionResult = null;
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
                if (disposed || packet == null) return;
                ApplyGroups(packet.Groups);
            }, "rpvoicechat:VoiceGroupUpdate");
        }

        private void ApplyGroups(IEnumerable<VoiceGroupStateEntry> groups)
        {
            groupsByName.Clear();
            groupByPlayerUid.Clear();

            foreach (var group in groups)
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
        }
    }
}
