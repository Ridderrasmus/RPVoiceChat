using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Networking;
using RPVoiceChat.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace RPVoiceChat.Server
{
    public class VoiceGroupManager : IDisposable
    {
        private const string SaveDataKey = "rpvoicechat:voicegroups";
        private const long InviteTimeoutMs = 30_000;

        private readonly ICoreServerAPI api;
        private readonly Dictionary<string, VoiceGroup> groupsByName = new Dictionary<string, VoiceGroup>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> groupByPlayerUid = new Dictionary<string, string>();

        private sealed class VoiceGroupPersistedEntry
        {
            public string Name { get; set; }
            public string OwnerPlayerUid { get; set; }
            public bool InviteOnly { get; set; }
            public List<string> Members { get; set; } = new List<string>();
            public List<string> PendingInvites { get; set; } = new List<string>();
            public Dictionary<string, long> PendingInviteTimestamps { get; set; } = new Dictionary<string, long>();
        }

        public VoiceGroupManager(ICoreServerAPI sapi)
        {
            api = sapi;
            api.Event.SaveGameLoaded += OnSaveGameLoaded;
            api.Event.GameWorldSave += OnGameWorldSave;
            LoadFromSaveGame();
        }

        public bool CreateGroup(string ownerPlayerUid, string groupName, out string message)
        {
            if (!IsValidGroupName(groupName, out message))
            {
                return false;
            }

            if (groupsByName.ContainsKey(groupName))
            {
                message = UIUtils.I18n("Command.Group.Error.AlreadyExists");
                return false;
            }

            if (groupByPlayerUid.ContainsKey(ownerPlayerUid))
            {
                message = UIUtils.I18n("Command.Group.Error.AlreadyInGroup");
                return false;
            }

            var group = new VoiceGroup(groupName, ownerPlayerUid);
            groupsByName[groupName] = group;
            groupByPlayerUid[ownerPlayerUid] = groupName;
            Persist();

            message = UIUtils.I18n("Command.Group.Create.Success", groupName);
            return true;
        }

        public bool JoinGroup(string playerUid, string groupName, out string message)
        {
            CleanupExpiredInvitesAndPersist();

            if (!groupsByName.TryGetValue(groupName, out var group))
            {
                message = UIUtils.I18n("Command.Group.Error.GroupNotFound");
                return false;
            }

            if (group.InviteOnly && !group.PendingInvites.Contains(playerUid))
            {
                message = UIUtils.I18n("Command.Group.Error.GroupInviteOnly", group.Name);
                return false;
            }

            if (groupByPlayerUid.TryGetValue(playerUid, out var currentGroupName))
            {
                if (string.Equals(currentGroupName, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    message = UIUtils.I18n("Command.Group.Join.AlreadyInTarget", groupName);
                    return false;
                }

                LeaveInternal(playerUid);
            }

            group.Members.Add(playerUid);
            group.PendingInvites.Remove(playerUid);
            groupByPlayerUid[playerUid] = group.Name;
            Persist();

            message = UIUtils.I18n("Command.Group.Join.Success", group.Name);
            return true;
        }

        public bool LeaveGroup(string playerUid, out string message)
        {
            if (!groupByPlayerUid.ContainsKey(playerUid))
            {
                message = UIUtils.I18n("Command.Group.Error.NotInGroup");
                return false;
            }

            LeaveInternal(playerUid);
            Persist();
            message = UIUtils.I18n("Command.Group.Leave.Success");
            return true;
        }

        public bool DeleteGroup(string requesterUid, string groupName, bool isAdmin, out string message)
        {
            if (!groupsByName.TryGetValue(groupName, out var group))
            {
                message = UIUtils.I18n("Command.Group.Error.GroupNotFound");
                return false;
            }

            bool isOwner = string.Equals(group.OwnerPlayerUid, requesterUid, StringComparison.Ordinal);
            if (!isOwner && !isAdmin)
            {
                message = UIUtils.I18n("Command.Group.Error.DeletePermission");
                return false;
            }

            foreach (var memberUid in group.Members.ToList())
            {
                groupByPlayerUid.Remove(memberUid);
            }

            groupsByName.Remove(group.Name);
            Persist();
            message = UIUtils.I18n("Command.Group.Delete.Success", group.Name);
            return true;
        }

        public bool KickPlayer(string requesterUid, string targetPlayerUid, bool isAdmin, out string message)
        {
            if (!groupByPlayerUid.TryGetValue(targetPlayerUid, out var targetGroupName) || !groupsByName.TryGetValue(targetGroupName, out var group))
            {
                message = UIUtils.I18n("Command.Group.Error.TargetNotInGroup");
                return false;
            }

            bool requesterInSameGroup = groupByPlayerUid.TryGetValue(requesterUid, out var requesterGroupName)
                && string.Equals(requesterGroupName, group.Name, StringComparison.OrdinalIgnoreCase);
            bool requesterOwnsGroup = requesterInSameGroup && string.Equals(group.OwnerPlayerUid, requesterUid, StringComparison.Ordinal);

            if (!isAdmin && !requesterOwnsGroup)
            {
                message = UIUtils.I18n("Command.Group.Error.KickPermission");
                return false;
            }

            if (string.Equals(group.OwnerPlayerUid, targetPlayerUid, StringComparison.Ordinal) && !isAdmin)
            {
                message = UIUtils.I18n("Command.Group.Error.CannotKickOwner");
                return false;
            }

            if (!group.Members.Remove(targetPlayerUid))
            {
                message = UIUtils.I18n("Command.Group.Error.TargetNotMember");
                return false;
            }

            groupByPlayerUid.Remove(targetPlayerUid);
            if (group.Members.Count == 0)
            {
                groupsByName.Remove(group.Name);
            }

            Persist();
            message = UIUtils.I18n("Command.Group.Kick.Success");
            return true;
        }

        public bool SetInviteOnly(string requesterUid, bool inviteOnly, bool isAdmin, out string message)
        {
            if (!groupByPlayerUid.TryGetValue(requesterUid, out var groupName) || !groupsByName.TryGetValue(groupName, out var group))
            {
                message = UIUtils.I18n("Command.Group.Error.NotInGroup");
                return false;
            }

            bool isOwner = string.Equals(group.OwnerPlayerUid, requesterUid, StringComparison.Ordinal);
            if (!isOwner && !isAdmin)
            {
                message = UIUtils.I18n("Command.Group.Error.InvitePermission");
                return false;
            }

            group.InviteOnly = inviteOnly;
            Persist();

            message = UIUtils.I18n(inviteOnly
                ? "Command.Group.InviteOnly.Success.Enabled"
                : "Command.Group.InviteOnly.Success.Disabled", group.Name);
            return true;
        }

        public bool InvitePlayer(string requesterUid, string targetPlayerUid, bool isAdmin, out string message)
        {
            CleanupExpiredInvitesAndPersist();

            if (!groupByPlayerUid.TryGetValue(requesterUid, out var groupName) || !groupsByName.TryGetValue(groupName, out var group))
            {
                message = UIUtils.I18n("Command.Group.Error.NotInGroup");
                return false;
            }

            bool isOwner = string.Equals(group.OwnerPlayerUid, requesterUid, StringComparison.Ordinal);
            if (!isOwner && !isAdmin)
            {
                message = UIUtils.I18n("Command.Group.Error.InvitePermission");
                return false;
            }

            if (group.Members.Contains(targetPlayerUid))
            {
                message = UIUtils.I18n("Command.Group.Error.TargetAlreadyMember");
                return false;
            }

            if (!group.PendingInvites.Add(targetPlayerUid))
            {
                message = UIUtils.I18n("Command.Group.Error.AlreadyInvited");
                return false;
            }

            group.PendingInviteTimestamps[targetPlayerUid] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Persist();
            message = UIUtils.I18n("Command.Group.Invite.Success", GetPlayerName(targetPlayerUid), group.Name);
            return true;
        }

        public bool AcceptInvite(string playerUid, string groupName, out string message)
        {
            CleanupExpiredInvitesAndPersist();

            if (!TryResolveGroupForInviteAction(playerUid, groupName, out var group, out message))
            {
                return false;
            }

            if (!group.PendingInvites.Contains(playerUid))
            {
                message = UIUtils.I18n("Command.Group.Error.NotInvited", group.Name);
                return false;
            }

            if (groupByPlayerUid.TryGetValue(playerUid, out var currentGroupName)
                && !string.Equals(currentGroupName, group.Name, StringComparison.OrdinalIgnoreCase))
            {
                LeaveInternal(playerUid);
            }

            group.PendingInvites.Remove(playerUid);
            group.PendingInviteTimestamps.Remove(playerUid);
            group.Members.Add(playerUid);
            groupByPlayerUid[playerUid] = group.Name;
            Persist();

            message = UIUtils.I18n("Command.Group.Accept.Success", group.Name);
            return true;
        }

        public bool DeclineInvite(string playerUid, string groupName, out string message)
        {
            CleanupExpiredInvitesAndPersist();

            if (!TryResolveGroupForInviteAction(playerUid, groupName, out var group, out message))
            {
                return false;
            }

            if (!group.PendingInvites.Remove(playerUid))
            {
                message = UIUtils.I18n("Command.Group.Error.NotInvited", group.Name);
                return false;
            }

            group.PendingInviteTimestamps.Remove(playerUid);

            Persist();
            message = UIUtils.I18n("Command.Group.Decline.Success", group.Name);
            return true;
        }

        private bool TryResolveGroupForInviteAction(string playerUid, string groupName, out VoiceGroup group, out string message)
        {
            group = null;
            message = string.Empty;

            if (!string.IsNullOrWhiteSpace(groupName))
            {
                if (!groupsByName.TryGetValue(groupName.Trim(), out group))
                {
                    message = UIUtils.I18n("Command.Group.Error.GroupNotFound");
                    return false;
                }

                return true;
            }

            if (!TryGetNewestInviteGroup(playerUid, out group))
            {
                message = UIUtils.I18n("Command.Group.Error.NoPendingInvites");
                return false;
            }

            return true;
        }

        private bool TryGetNewestInviteGroup(string playerUid, out VoiceGroup newestGroup)
        {
            newestGroup = null;
            long newestTimestamp = long.MinValue;

            foreach (var group in groupsByName.Values)
            {
                if (!group.PendingInvites.Contains(playerUid))
                {
                    continue;
                }

                long inviteTimestamp = group.PendingInviteTimestamps.TryGetValue(playerUid, out var timestamp)
                    ? timestamp
                    : 0L;

                if (newestGroup == null || inviteTimestamp > newestTimestamp)
                {
                    newestGroup = group;
                    newestTimestamp = inviteTimestamp;
                }
            }

            return newestGroup != null;
        }

        private void CleanupExpiredInvitesAndPersist()
        {
            if (CleanupExpiredInvites())
            {
                Persist();
            }
        }

        private bool CleanupExpiredInvites()
        {
            bool changed = false;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var group in groupsByName.Values)
            {
                var expiredInvitees = group.PendingInvites
                    .Where(invitedUid =>
                    {
                        long timestamp = group.PendingInviteTimestamps.TryGetValue(invitedUid, out var inviteTimestamp)
                            ? inviteTimestamp
                            : 0L;
                        return now - timestamp > InviteTimeoutMs;
                    })
                    .ToList();

                foreach (var invitedUid in expiredInvitees)
                {
                    group.PendingInvites.Remove(invitedUid);
                    group.PendingInviteTimestamps.Remove(invitedUid);
                    changed = true;
                }

                var danglingTimestamps = group.PendingInviteTimestamps.Keys
                    .Where(uid => !group.PendingInvites.Contains(uid))
                    .ToList();

                foreach (var uid in danglingTimestamps)
                {
                    group.PendingInviteTimestamps.Remove(uid);
                    changed = true;
                }
            }

            return changed;
        }

        public bool TryGetGroupName(string playerUid, out string groupName)
        {
            return groupByPlayerUid.TryGetValue(playerUid, out groupName);
        }

        public bool IsGroupOwner(string playerUid, string groupName)
        {
            return groupsByName.TryGetValue(groupName, out var group)
                && string.Equals(group.OwnerPlayerUid, playerUid, StringComparison.Ordinal);
        }

        public HashSet<string> GetGroupMembersForPlayer(string playerUid)
        {
            if (!groupByPlayerUid.TryGetValue(playerUid, out var groupName))
            {
                return new HashSet<string>();
            }

            if (!groupsByName.TryGetValue(groupName, out var group))
            {
                return new HashSet<string>();
            }

            return new HashSet<string>(group.Members);
        }

        public List<VoiceGroupStateEntry> BuildStateEntries()
        {
            CleanupExpiredInvitesAndPersist();

            return groupsByName.Values
                .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => new VoiceGroupStateEntry(
                    group.Name,
                    group.OwnerPlayerUid,
                    group.InviteOnly,
                    group.Members.OrderBy(member => member, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();
        }

        public string GetGroupSummaryForPlayer(string playerUid)
        {
            if (!groupByPlayerUid.TryGetValue(playerUid, out var groupName) || !groupsByName.TryGetValue(groupName, out var group))
            {
                return UIUtils.I18n("Command.Group.Error.NotInGroup");
            }

            var memberNames = group.Members
                .Select(GetPlayerName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
            string ownerName = GetPlayerName(group.OwnerPlayerUid);

            return UIUtils.I18n("Command.Group.My.Success", group.Name, ownerName, string.Join(", ", memberNames));
        }

        public string GetAllGroupsSummary()
        {
            if (groupsByName.Count == 0)
            {
                return UIUtils.I18n("Command.Group.List.Empty");
            }

            var groups = groupsByName.Values
                .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => $"{group.Name} ({group.Members.Count})");

            return UIUtils.I18n("Command.Group.List.Success", string.Join(", ", groups));
        }

        private string GetPlayerName(string playerUid)
        {
            IPlayer player = api.World.PlayerByUid(playerUid);
            if (player != null)
            {
                return player.PlayerName;
            }

            var offlinePlayer = api.World.AllPlayers.FirstOrDefault(playerInfo => playerInfo.PlayerUID == playerUid);
            return offlinePlayer?.PlayerName ?? playerUid;
        }

        private bool IsValidGroupName(string groupName, out string message)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                message = UIUtils.I18n("Command.Group.Error.EmptyName");
                return false;
            }

            groupName = groupName.Trim();
            if (groupName.Length < 3 || groupName.Length > 24)
            {
                message = UIUtils.I18n("Command.Group.Error.NameLength", 3, 24);
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void LeaveInternal(string playerUid)
        {
            if (!groupByPlayerUid.TryGetValue(playerUid, out var groupName))
            {
                return;
            }

            if (!groupsByName.TryGetValue(groupName, out var group))
            {
                groupByPlayerUid.Remove(playerUid);
                return;
            }

            group.Members.Remove(playerUid);
            group.PendingInvites.Remove(playerUid);
            group.PendingInviteTimestamps.Remove(playerUid);
            groupByPlayerUid.Remove(playerUid);

            if (group.Members.Count == 0)
            {
                groupsByName.Remove(group.Name);
                return;
            }

            if (string.Equals(group.OwnerPlayerUid, playerUid, StringComparison.Ordinal))
            {
                group.OwnerPlayerUid = group.Members.OrderBy(member => member, StringComparer.OrdinalIgnoreCase).First();
            }
        }

        private void LoadGroups()
        {
            try
            {
                byte[] data = api.WorldManager.SaveGame.GetData(SaveDataKey);
                var groups = data == null
                    ? new List<VoiceGroupPersistedEntry>()
                    : SerializerUtil.Deserialize<List<VoiceGroupPersistedEntry>>(data);

                groupsByName.Clear();
                groupByPlayerUid.Clear();

                foreach (var loadedGroup in groups)
                {
                    if (string.IsNullOrWhiteSpace(loadedGroup?.Name) || string.IsNullOrWhiteSpace(loadedGroup.OwnerPlayerUid))
                    {
                        continue;
                    }

                    loadedGroup.Members ??= new List<string>();
                    loadedGroup.PendingInvites ??= new List<string>();
                    loadedGroup.PendingInviteTimestamps ??= new Dictionary<string, long>();
                    loadedGroup.Members.Add(loadedGroup.OwnerPlayerUid);

                    var group = new VoiceGroup
                    {
                        Name = loadedGroup.Name,
                        OwnerPlayerUid = loadedGroup.OwnerPlayerUid,
                        InviteOnly = loadedGroup.InviteOnly,
                        Members = new HashSet<string>(loadedGroup.Members),
                        PendingInvites = new HashSet<string>(loadedGroup.PendingInvites),
                        PendingInviteTimestamps = new Dictionary<string, long>(loadedGroup.PendingInviteTimestamps)
                    };

                    group.PendingInvites.RemoveWhere(memberUid => group.Members.Contains(memberUid));
                    foreach (var invitedUid in group.PendingInvites)
                    {
                        if (!group.PendingInviteTimestamps.ContainsKey(invitedUid))
                        {
                            group.PendingInviteTimestamps[invitedUid] = 0L;
                        }
                    }

                    var unknownTimestampUids = group.PendingInviteTimestamps.Keys
                        .Where(uid => !group.PendingInvites.Contains(uid))
                        .ToList();
                    foreach (var unknownUid in unknownTimestampUids)
                    {
                        group.PendingInviteTimestamps.Remove(unknownUid);
                    }

                    groupsByName[group.Name] = group;
                    foreach (var member in loadedGroup.Members)
                    {
                        if (!groupByPlayerUid.ContainsKey(member))
                        {
                            groupByPlayerUid[member] = group.Name;
                        }
                    }
                }

                CleanupExpiredInvites();
            }
            catch (Exception e)
            {
                Logger.server.Warning($"Error loading voice groups: {e.Message}");
                groupsByName.Clear();
                groupByPlayerUid.Clear();
            }
        }

        private void Persist()
        {
            try
            {
                var serializedGroups = groupsByName.Values
                    .Select(group => new VoiceGroupPersistedEntry
                    {
                        Name = group.Name,
                        OwnerPlayerUid = group.OwnerPlayerUid,
                        InviteOnly = group.InviteOnly,
                        Members = group.Members.ToList(),
                        PendingInvites = group.PendingInvites.ToList(),
                        PendingInviteTimestamps = group.PendingInviteTimestamps
                    })
                    .ToList();

                byte[] data = SerializerUtil.Serialize(serializedGroups);
                api.WorldManager.SaveGame.StoreData(SaveDataKey, data);
            }
            catch (Exception e)
            {
                Logger.server.Error($"Error saving voice groups: {e.Message}");
            }
        }

        private void OnSaveGameLoaded()
        {
            LoadFromSaveGame();
        }

        private void OnGameWorldSave()
        {
            Persist();
        }

        private void LoadFromSaveGame()
        {
            LoadGroups();
        }

        public void Dispose()
        {
            try
            {
                api.Event.SaveGameLoaded -= OnSaveGameLoaded;
                api.Event.GameWorldSave -= OnGameWorldSave;
            }
            catch (Exception e)
            {
                Logger.server.Warning($"Error disposing voice group manager events: {e.Message}");
            }
        }
    }
}
