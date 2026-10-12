using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Config;
using RPVoiceChat.Networking;
using RPVoiceChat.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RPVoiceChat.Server
{
    public partial class GameServer
    {
        private readonly Dictionary<string, long> lastGroupRequest = new();

        private void OnVoiceGroupAction(IServerPlayer player, VoiceGroupActionPacket packet)
        {
            if (player == null || packet == null || player.ConnectionState != EnumClientState.Playing) return;
            // Identity and privileges always come from the authenticated connection.
            long now = Environment.TickCount64;
            if (lastGroupRequest.TryGetValue(player.PlayerUID, out long last) && now - last < 150)
            {
                SendGroupActionResult(player, false, UIUtils.I18n("Gui.VoiceGroups.PleaseWait"));
                return;
            }
            lastGroupRequest[player.PlayerUID] = now;

            if (packet.Action == VoiceGroupAction.Refresh)
            {
                SendVoiceGroupUiState(player);
                return;
            }

            bool admin = player.HasPrivilege(Privilege.controlserver);
            if (!player.HasPrivilege(Privilege.chat))
            {
                SendGroupActionResult(player, false, UIUtils.I18n("Gui.VoiceGroups.NoPermission"));
                return;
            }
            if (packet.Action == VoiceGroupAction.SetEnabled)
            {
                if (!admin)
                {
                    SendGroupActionResult(player, false, UIUtils.I18n("Gui.VoiceGroups.NoPermission"));
                    return;
                }
                ModConfig.ServerConfig.VoiceGroupsEnabled = packet.Value;
                ModConfig.SaveServer(api);
                SendGroupActionResult(player, true, UIUtils.I18n(packet.Value
                    ? "Command.VoiceGroups.Success.Enabled" : "Command.VoiceGroups.Success.Disabled"));
                NotifyAllPlayersVoiceGroupsUpdated();
                return;
            }
            if (!IsVoiceGroupsEnabled())
            {
                SendGroupActionResult(player, false, UIUtils.I18n("Command.Group.Error.Disabled"));
                SendVoiceGroupUiState(player);
                return;
            }

            string group = packet.GroupName?.Trim() ?? "";
            string target = packet.TargetPlayerUid ?? "";
            if (group.Length > 24 || target.Length > 128 || !Enum.IsDefined(typeof(VoiceGroupAction), packet.Action))
            {
                SendGroupActionResult(player, false, UIUtils.I18n("Gui.VoiceGroups.InvalidRequest"));
                return;
            }

            bool success;
            string message;
            string uid = player.PlayerUID;
            switch (packet.Action)
            {
                case VoiceGroupAction.Create: success = voiceGroupManager.CreateGroup(uid, group, out message); break;
                case VoiceGroupAction.Join: success = voiceGroupManager.JoinGroup(uid, group, out message); break;
                case VoiceGroupAction.Leave: success = voiceGroupManager.LeaveGroup(uid, out message); break;
                case VoiceGroupAction.Delete: success = voiceGroupManager.DeleteGroup(uid, group, admin, out message); break;
                case VoiceGroupAction.Kick: success = voiceGroupManager.KickPlayer(uid, target, admin, out message); break;
                case VoiceGroupAction.Invite:
                    var invitee = api.World.PlayerByUid(target) as IServerPlayer;
                    if (invitee?.ConnectionState != EnumClientState.Playing)
                    {
                        success = false;
                        message = UIUtils.I18n("Command.Group.Error.PlayerNotFound");
                    }
                    else success = voiceGroupManager.InvitePlayer(uid, target, admin, out message);
                    break;
                case VoiceGroupAction.SetInviteOnly: success = voiceGroupManager.SetInviteOnly(uid, packet.Value, admin, out message); break;
                case VoiceGroupAction.Accept: success = voiceGroupManager.AcceptInvite(uid, group, out message); break;
                case VoiceGroupAction.Decline: success = voiceGroupManager.DeclineInvite(uid, group, out message); break;
                default:
                    success = false;
                    message = UIUtils.I18n("Gui.VoiceGroups.InvalidRequest");
                    break;
            }
            SendGroupActionResult(player, success, message);
            if (success) NotifyAllPlayersVoiceGroupsUpdated();
            else SendVoiceGroupUiState(player);
        }

        private void SendGroupActionResult(IServerPlayer player, bool success, string message)
        {
            voiceGroupChannel.SendPacket(new VoiceGroupActionResultPacket { Success = success, Message = message }, player);
        }

        private void SendVoiceGroupUiState(IServerPlayer player)
        {
            bool enabled = IsVoiceGroupsEnabled();
            var groups = enabled ? voiceGroupManager.BuildStateEntries() : new List<VoiceGroupStateEntry>();
            var memberUids = new HashSet<string>(groups.SelectMany(group => group.Members));
            var players = api.World.AllPlayers
                .Where(p => memberUids.Contains(p.PlayerUID) || (p as IServerPlayer)?.ConnectionState == EnumClientState.Playing)
                .Select(p => new VoiceGroupPlayerEntry
                {
                    Uid = p.PlayerUID,
                    Name = p.PlayerName,
                    Online = (p as IServerPlayer)?.ConnectionState == EnumClientState.Playing
                }).ToList();

            voiceGroupChannel.SendPacket(new VoiceGroupUiStatePacket
            {
                Enabled = enabled,
                CanManageServer = player.HasPrivilege(Privilege.controlserver),
                Groups = groups,
                // Invitations are private to their recipient.
                Invitations = enabled ? voiceGroupManager.GetPendingInvitations(player.PlayerUID) : new List<string>(),
                Players = players
            }, player);
        }

        private void NotifyVoiceGroupUiPlayers()
        {
            foreach (var player in api.World.AllOnlinePlayers.OfType<IServerPlayer>())
            {
                if (player.ConnectionState == EnumClientState.Playing) SendVoiceGroupUiState(player);
            }
        }
    }
}
