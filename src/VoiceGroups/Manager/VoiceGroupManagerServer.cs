using RPVoiceChat.VoiceGroups.Packets;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Server;

namespace RPVoiceChat.VoiceGroups.Manager
{
    public class VoiceGroupManagerServer : VoiceGroupManagerCommon
    {
        private Dictionary<string, VoiceGroup> voiceGroups = new();
        private Dictionary<string, string> playerToGroup = new();

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

            _groupNetChannel.
                SetMessageHandler<VoiceGroupRequest>(HandleVoiceGroupRequest);
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

            IServerPlayer[] members = _sapi.World.AllOnlinePlayers.Where(p => playerToGroup[p.PlayerUID] == group.Name).Select(p => (IServerPlayer)p).ToArray();

            _groupNetChannel.SendPacket<VoiceGroup>(group, members);
        }


        #region Command Handlers

        private TextCommandResult VoiceGroupAcceptInvite(TextCommandCallingArgs args)
        {
            throw new NotImplementedException();
        }

        private TextCommandResult VoiceGroupListGroups(TextCommandCallingArgs args)
        {
            throw new NotImplementedException();
        }

        private TextCommandResult VoiceGroupLeaveGroup(TextCommandCallingArgs args)
        {
            throw new NotImplementedException();
        }

        private TextCommandResult VoiceGroupInvite(TextCommandCallingArgs args)
        {
            throw new NotImplementedException();
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
            group = new VoiceGroup(groupName, owner.PlayerUID);
            voiceGroups[groupName] = group;
            playerToGroup[owner.PlayerUID] = groupName;
            
            SendGroupUpdate(group);

            OnGroupUpdated?.Invoke(group);

            return true;
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
            _sapi.WorldManager.SaveGame.StoreData<Dictionary<string, VoiceGroup>>("voice-groups", voiceGroups);
        }

        private void LoadState()
        {
            // Load the voice groups and player-to-group mappings from persistent storage
            var savedGroups = _sapi.WorldManager.SaveGame.GetData<Dictionary<string, VoiceGroup>>("voice-groups");
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
        }

    }
}
