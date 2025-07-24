using RPVoiceChat.VoiceGroups.Packets;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace RPVoiceChat.VoiceGroups.Manager
{
    public class VoiceGroupManagerServer : VoiceGroupManagerCommon
    {
        private Dictionary<string, VoiceGroup> voiceGroups = new();
        private Dictionary<string, string> playerToGroup = new();
        private Dictionary<string, List<string>> playerInvites = new(); // playerId -> list of group names they're invited to

        private event Action<VoiceGroup> OnGroupUpdated;

        private IServerNetworkChannel _groupNetChannel;

        private ICoreServerAPI _sapi;

        public VoiceGroupManagerServer(ICoreServerAPI sapi) : base(sapi)
        {
            _sapi = sapi;

            RegisterConfig();
            RegisterCommands();
            RegisterNetworkChannel();

            OnGroupUpdated += (g) => { SaveState(); };

            LoadState();
        }

        private void RegisterConfig()
        {
            // Register configuration settings for voice groups
            WorldConfig.Set("allow-group-voicechat", WorldConfig.GetBool("allow-group-voicechat", false));
        }

        private void RegisterCommands()
        {
            // Register commands for managing voice groups
            var parsers = _sapi.ChatCommands.Parsers;

            _sapi.ChatCommands.GetOrCreate("voicegroup")
                .WithAlias("voicechatgroup", "vcgroup", "vcg")
                .RequiresPrivilege(Privilege.manageplayergroups)
                .WithAdditionalInformation(UIUtils.I18n("Command.VoiceGroup.Help"))
                .WithPreCondition(args =>
                {
                    if (!WorldConfig.GetBool("allow-group-voicechat", false))
                    {
                        return TextCommandResult.Error(UIUtils.I18n("Command.AllowGroupVoiceChat.Disabled"));
                    }
                    return TextCommandResult.Success();
                })
                .BeginSub("create")
                    .WithDesc(UIUtils.I18n("Command.VoiceGroup.Create.Desc"))
                    .WithAdditionalInformation(UIUtils.I18n("Command.VoiceGroup.Create.Help"))
                    .WithArgs(parsers.OptionalWord("name"))
                    .HandleWith(VoiceGroupCreateGroup)
                .EndSub()
                .BeginSub("invite")
                    .WithDesc(UIUtils.I18n("Command.VoiceGroup.Invite.Desc"))
                    .WithAdditionalInformation(UIUtils.I18n("Command.VoiceGroup.Invite.Help"))
                    .WithArgs(parsers.OnlinePlayer("player"))
                    .HandleWith(VoiceGroupInvite)
                .EndSub()
                .BeginSub("accept")
                    .WithDesc(UIUtils.I18n("Command.VoiceGroup.Accept.Desc"))
                    .WithArgs(parsers.OptionalWord("name"))
                    .HandleWith(VoiceGroupAcceptInvite)
                .EndSub()
                .BeginSub("leave")
                    .WithDesc(UIUtils.I18n("Command.VoiceGroup.Leave.Desc"))
                    .HandleWith(VoiceGroupLeaveGroup)
                .EndSub()
                .BeginSub("list")
                    .WithDesc(UIUtils.I18n("Command.VoiceGroup.List.Desc"))
                    .HandleWith(VoiceGroupListGroups)
                .EndSub();
        }

        private void RegisterNetworkChannel()
        {
            // Register the network channel for voice group communication
            _groupNetChannel = _sapi.Network.GetChannel(_rpvcGroupNetworkChannelName);

            _groupNetChannel.SetMessageHandler<VoiceGroupRequest>(HandleVoiceGroupRequest);
        }

        private void HandleVoiceGroupRequest(IServerPlayer fromPlayer, VoiceGroupRequest packet)
        {
            if (packet == null || string.IsNullOrEmpty(packet.GroupName))
            {
                Logger.server.Error("Received invalid VoiceGroupRequest from player {0}", fromPlayer.PlayerUID);
                return;
            }
            
            if (!voiceGroups.TryGetValue(packet.GroupName, out VoiceGroup group))
            {
                Logger.server.Error("Voice group '{0}' not found for player {1}", packet.GroupName, fromPlayer.PlayerUID);
                return;
            }
            
            // Send the group data back to the requesting player
            _groupNetChannel.SendPacket(group, fromPlayer);
        }

        private void SendGroupUpdate(VoiceGroup group)
        {
            // Broadcast the group update to all players in the group
            if (group == null) return;

            var members = group.Members.Select(memberId => _sapi.World.PlayerByUid(memberId))
                                     .Where(player => player is IServerPlayer)
                                     .Cast<IServerPlayer>()
                                     .ToArray();

            if (members.Length > 0)
            {
                _groupNetChannel.SendPacket(group, members);
            }
        }

        #region Command Handlers

        private TextCommandResult VoiceGroupAcceptInvite(TextCommandCallingArgs args)
        {
            string playerId = args.Caller.Player.PlayerUID;
            string groupName = (string)args[0];

            // If no group name specified, accept the first available invite
            if (string.IsNullOrWhiteSpace(groupName))
            {
                if (!playerInvites.TryGetValue(playerId, out var invites) || invites.Count == 0)
                {
                    return TextCommandResult.Error("You have no pending voice group invites.");
                }
                groupName = invites[0];
            }

            // Check if player has an invite to this group
            if (!playerInvites.TryGetValue(playerId, out var playerInviteList) || 
                !playerInviteList.Contains(groupName))
            {
                return TextCommandResult.Error($"You don't have an invite to voice group '{groupName}'.");
            }

            // Check if group still exists
            if (!voiceGroups.TryGetValue(groupName, out var group))
            {
                // Remove invalid invite
                playerInviteList.Remove(groupName);
                return TextCommandResult.Error($"Voice group '{groupName}' no longer exists.");
            }

            // Remove player from current group if they're in one
            if (playerToGroup.TryGetValue(playerId, out var currentGroupName))
            {
                LeaveGroup(playerId);
            }

            // Add player to the group
            var membersList = group.Members.ToList();
            membersList.Add(playerId);
            group.Members = membersList.ToArray();
            
            playerToGroup[playerId] = groupName;
            playerInviteList.Remove(groupName);

            // Update and notify
            SendGroupUpdate(group);
            OnGroupUpdated?.Invoke(group);

            return TextCommandResult.Success(UIUtils.I18n("Command.VoiceGroup.Accept.Success", groupName));
        }

        private TextCommandResult VoiceGroupListGroups(TextCommandCallingArgs args)
        {
            string playerId = args.Caller.Player.PlayerUID;

            if (!playerInvites.TryGetValue(playerId, out var invites) || invites.Count == 0)
            {
                return TextCommandResult.Success("You have no pending voice group invites.");
            }

            string inviteList = string.Join("\n", invites.Select(groupName => $"- {groupName}"));
            return TextCommandResult.Success(UIUtils.I18n("Command.VoiceGroup.List.Success", inviteList));
        }

        private TextCommandResult VoiceGroupLeaveGroup(TextCommandCallingArgs args)
        {
            string playerId = args.Caller.Player.PlayerUID;

            if (!playerToGroup.TryGetValue(playerId, out var groupName))
            {
                return TextCommandResult.Error("You are not currently in a voice group.");
            }

            if (!voiceGroups.TryGetValue(groupName, out var group))
            {
                // Clean up orphaned player mapping
                playerToGroup.Remove(playerId);
                return TextCommandResult.Error("Your voice group no longer exists.");
            }

            LeaveGroup(playerId);
            
            return TextCommandResult.Success(UIUtils.I18n("Command.VoiceGroup.Leave.Success", groupName));
        }

        private TextCommandResult VoiceGroupInvite(TextCommandCallingArgs args)
        {
            IServerPlayer targetPlayer = (IServerPlayer)args[0];
            string inviterId = args.Caller.Player.PlayerUID;

            // Check if inviter is in a group
            if (!playerToGroup.TryGetValue(inviterId, out var groupName))
            {
                return TextCommandResult.Error("You must be in a voice group to invite others. Create one first with '/voicegroup create'.");
            }

            if (!voiceGroups.TryGetValue(groupName, out var group))
            {
                // Clean up orphaned player mapping
                playerToGroup.Remove(inviterId);
                return TextCommandResult.Error("Your voice group no longer exists.");
            }

            // Check if inviter is the group owner
            if (group.Owner != inviterId)
            {
                return TextCommandResult.Error("Only the group owner can invite new members.");
            }

            // Check if target player is already in the group
            if (group.Members.Contains(targetPlayer.PlayerUID))
            {
                return TextCommandResult.Error($"{targetPlayer.PlayerName} is already in your voice group.");
            }

            // Check if target player is already in another group
            if (playerToGroup.ContainsKey(targetPlayer.PlayerUID))
            {
                return TextCommandResult.Error($"{targetPlayer.PlayerName} is already in another voice group.");
            }

            // Add invite
            if (!playerInvites.TryGetValue(targetPlayer.PlayerUID, out var invites))
            {
                invites = new List<string>();
                playerInvites[targetPlayer.PlayerUID] = invites;
            }

            if (!invites.Contains(groupName))
            {
                invites.Add(groupName);
            }

            // Notify both players
            ((IServerPlayer)args.Caller).SendMessage(GlobalConstants.InfoLogChatGroup, 
                UIUtils.I18n("Command.VoiceGroup.Invite.Success", targetPlayer.PlayerName), 
                EnumChatType.Notification);

            targetPlayer.SendMessage(GlobalConstants.InfoLogChatGroup,
                $"You have been invited to join voice group '{groupName}' by {args.Caller.Player.PlayerName}. " +
                $"Use '/voicegroup accept {groupName}' to join or '/voicegroup list' to see all invites.",
                EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult VoiceGroupCreateGroup(TextCommandCallingArgs args)
        {
            string groupName = (string)args[0] ?? "";

            if (string.IsNullOrWhiteSpace(groupName))
                groupName = args.Caller.Player.PlayerName;

            if (groupName.Length < 3 || groupName.Length > 20)
                return TextCommandResult.Error(UIUtils.I18n("Command.VoiceGroup.Create.InvalidName", groupName));

            if (!TryCreateGroup(groupName, args.Caller.Player, out VoiceGroup group))
                return TextCommandResult.Error(UIUtils.I18n("Command.VoiceGroup.Create.Failure", groupName));

            return TextCommandResult.Success(UIUtils.I18n("Command.VoiceGroup.Create.Success", group.Name));
        }
        #endregion

        #region Group Management

        private bool TryCreateGroup(string groupName, IPlayer owner, out VoiceGroup group)
        {
            if (voiceGroups.ContainsKey(groupName))
            {
                Logger.server.Debug("Voice group '{0}' already exists.", groupName);
                group = null;
                return false;
            }

            // Check if owner is already in a group
            if (playerToGroup.ContainsKey(owner.PlayerUID))
            {
                Logger.server.Debug("Player '{0}' is already in a voice group.", owner.PlayerUID);
                group = null;
                return false;
            }

            group = new VoiceGroup(groupName, owner.PlayerUID);
            voiceGroups[groupName] = group;
            playerToGroup[owner.PlayerUID] = groupName;
            
            SendGroupUpdate(group);
            OnGroupUpdated?.Invoke(group);

            return true;
        }

        private void LeaveGroup(string playerId)
        {
            if (!playerToGroup.TryGetValue(playerId, out var groupName))
                return;

            if (!voiceGroups.TryGetValue(groupName, out var group))
            {
                playerToGroup.Remove(playerId);
                return;
            }

            // Remove player from group
            var membersList = group.Members.ToList();
            membersList.Remove(playerId);
            group.Members = membersList.ToArray();
            playerToGroup.Remove(playerId);

            // If group is empty or owner left, disband the group
            if (group.Members.Length == 0 || group.Owner == playerId)
            {
                // Notify remaining members that group is disbanded
                group.Disbanded = true;
                SendGroupUpdate(group);

                // Clean up
                foreach (var memberId in group.Members)
                {
                    playerToGroup.Remove(memberId);
                }
                voiceGroups.Remove(groupName);

                // Remove all invites to this group
                foreach (var inviteList in playerInvites.Values)
                {
                    inviteList.Remove(groupName);
                }
            }
            else
            {
                // Just update the group
                SendGroupUpdate(group);
            }

            OnGroupUpdated?.Invoke(group);
        }

        #endregion

        #region Helper Methods
        internal bool InSameGroup(string playerUID1, string playerUID2)
        {
            if (string.IsNullOrEmpty(playerUID1) || string.IsNullOrEmpty(playerUID2))
                return false;

            if (!playerToGroup.TryGetValue(playerUID1, out string group1) ||
                !playerToGroup.TryGetValue(playerUID2, out string group2))            
                return false;

            return group1 == group2;
        }
        #endregion

        private void SaveState()
        {
            // Save the current state of the voice groups to persistent storage
            _sapi.WorldManager.SaveGame.StoreData("voice-groups", voiceGroups);
            _sapi.WorldManager.SaveGame.StoreData("voice-group-invites", playerInvites);
        }

        private void LoadState()
        {
            // Load the voice groups and player-to-group mappings from persistent storage
            var savedGroups = _sapi.WorldManager.SaveGame.GetData<Dictionary<string, VoiceGroup>>("voice-groups");
            var savedInvites = _sapi.WorldManager.SaveGame.GetData<Dictionary<string, List<string>>>("voice-group-invites");
            
            if (savedGroups != null)
            {
                voiceGroups = savedGroups;
                playerToGroup.Clear();
                foreach (var group in voiceGroups.Values)
                {
                    foreach (var member in group.Members)
                    {
                        playerToGroup[member] = group.Name;
                    }
                }
            }

            if (savedInvites != null)
            {
                playerInvites = savedInvites;
            }
        }
    }
}
