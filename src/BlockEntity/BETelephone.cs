using System;
using System.Linq;
using System.Text;
using RPVoiceChat.Config;
using RPVoiceChat.Gui;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using RPVoiceChat.Util;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityTelephone : BEWireNode, INetworkRoot, IWireTypedNode, ITelephoneVoiceEndpoint
    {
        private enum TelephoneCallState
        {
            Idle,
            Ringing,
            InCall
        }

        private const int RingDurationMs = 3000;
        private const int InCallDurationMs = 5000;
        private TelephoneMenuDialog dialog;
        private TelephoneCallState callState = TelephoneCallState.Idle;
        private long stateUntilMs;
        private string phoneNumber = "";
        private string targetNumber = "";
        private bool composeManagedBySwitchboard;
        private bool composeEnabled;
        private string composeDisabledReasonLangKey = "Telegraph.Settings.DisabledNoPower";
        private string activeCallerPlayerUid = "";
        private string incomingCallerPlayerUid = "";
        private BlockPos incomingCallerTelephonePos;
        private long originalCreatedNetworkID = 0;

        public override bool IsActiveEndpoint => true;
        protected override int MaxConnections => 1;
        public WireNodeKind WireNodeKind => WireNodeKind.Telephone;
        public int VoiceEmissionRangeBlocks => ServerConfigManager.TelephoneAudibleDistance;
        public long CreatedNetworkID => originalCreatedNetworkID;

        public override void OnNetworkCreated(long networkID)
        {
            base.OnNetworkCreated(networkID);
            if (originalCreatedNetworkID == 0)
            {
                originalCreatedNetworkID = networkID;
                MarkDirty();
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 250);
                if (NetworkUID != 0)
                {
                    WireNetworkHandler.RefreshTelegraphRoutingSnapshot(NetworkUID);
                }
            }

            // Telephone uses ringable behavior out-of-the-box.
            var ringable = GetBehavior<BEBehaviorRingable>();
            if (ringable != null && string.IsNullOrWhiteSpace(ringable.BellPartCode))
            {
                ringable.BellPartCode = "smallbellparts-silver";
            }
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                if (TryAcceptIncomingCall(byPlayer))
                {
                    return true;
                }
                StartCall(byPlayer);
                return true;
            }

            if (Api is not ICoreClientAPI capi)
            {
                return true;
            }

            if (dialog?.IsOpened() == true) return true;
            dialog = new TelephoneMenuDialog(capi, this);
            dialog.TryOpen();

            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            phoneNumber = tree.GetString("rpvc:phoneNumber", "");
            targetNumber = tree.GetString("rpvc:targetNumber", "");
            composeManagedBySwitchboard = tree.GetBool("rpvc:telephoneManaged", false);
            composeEnabled = tree.GetBool("rpvc:telephoneComposeEnabled", false);
            composeDisabledReasonLangKey = tree.GetString("rpvc:telephoneDisabledReason", "Telegraph.Settings.DisabledNoPower");
            callState = (TelephoneCallState)tree.GetInt("rpvc:telephoneState", (int)TelephoneCallState.Idle);
            stateUntilMs = tree.GetLong("rpvc:telephoneStateUntilMs", 0);
            activeCallerPlayerUid = tree.GetString("rpvc:telephoneCallerUid", "");
            incomingCallerPlayerUid = tree.GetString("rpvc:telephoneIncomingCallerUid", "");
            int incomingX = tree.GetInt("rpvc:telephoneIncomingCallerPosX", int.MinValue);
            int incomingY = tree.GetInt("rpvc:telephoneIncomingCallerPosY", int.MinValue);
            int incomingZ = tree.GetInt("rpvc:telephoneIncomingCallerPosZ", int.MinValue);
            incomingCallerTelephonePos = incomingX == int.MinValue ? null : new BlockPos(incomingX, incomingY, incomingZ);
            long savedOriginalCreatedNetworkID = tree.GetLong("rpvc:telephoneOriginalCreatedNetworkID", 0);
            if (savedOriginalCreatedNetworkID != 0)
            {
                originalCreatedNetworkID = savedOriginalCreatedNetworkID;
            }
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("rpvc:phoneNumber", phoneNumber ?? "");
            tree.SetString("rpvc:targetNumber", targetNumber ?? "");
            tree.SetBool("rpvc:telephoneManaged", composeManagedBySwitchboard);
            tree.SetBool("rpvc:telephoneComposeEnabled", composeEnabled);
            tree.SetString("rpvc:telephoneDisabledReason", composeDisabledReasonLangKey ?? "Telegraph.Settings.DisabledNoPower");
            tree.SetInt("rpvc:telephoneState", (int)callState);
            tree.SetLong("rpvc:telephoneStateUntilMs", stateUntilMs);
            tree.SetString("rpvc:telephoneCallerUid", activeCallerPlayerUid ?? "");
            tree.SetString("rpvc:telephoneIncomingCallerUid", incomingCallerPlayerUid ?? "");
            if (incomingCallerTelephonePos != null)
            {
                tree.SetInt("rpvc:telephoneIncomingCallerPosX", incomingCallerTelephonePos.X);
                tree.SetInt("rpvc:telephoneIncomingCallerPosY", incomingCallerTelephonePos.Y);
                tree.SetInt("rpvc:telephoneIncomingCallerPosZ", incomingCallerTelephonePos.Z);
            }
            else
            {
                tree.SetInt("rpvc:telephoneIncomingCallerPosX", int.MinValue);
                tree.SetInt("rpvc:telephoneIncomingCallerPosY", int.MinValue);
                tree.SetInt("rpvc:telephoneIncomingCallerPosZ", int.MinValue);
            }
            tree.SetLong("rpvc:telephoneOriginalCreatedNetworkID", originalCreatedNetworkID);
        }

        private void RingFromNetwork(IPlayer sourcePlayer)
        {
            var ringable = GetBehavior<BEBehaviorRingable>();
            if (ringable == null) return;

            double cooldown = ServerConfigManager.BellRingCooldownSeconds;
            if (ringable.LastRung != null && ringable.LastRung >= DateTime.Now.AddSeconds(-cooldown))
            {
                return;
            }

            ringable.LastRung = DateTime.Now;
            SetState(TelephoneCallState.Ringing, RingDurationMs);

            int rand = Api.World.Rand.Next(1, 3);
            Api.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, $"sounds/block/callbell/callbell_{rand}.ogg"),
                Pos.X + 0.5,
                Pos.Y + 0.5,
                Pos.Z + 0.5,
                sourcePlayer,
                false,
                2f,
                0.6f
            );

            MarkDirty(true);
        }

        private void OnServerTick(float _dt)
        {
            if (callState == TelephoneCallState.Idle || Api?.World == null) return;
            if (Api.World.ElapsedMilliseconds < stateUntilMs) return;

            callState = TelephoneCallState.Idle;
            stateUntilMs = 0;
            incomingCallerPlayerUid = "";
            incomingCallerTelephonePos = null;
            if (!string.IsNullOrWhiteSpace(activeCallerPlayerUid))
            {
                Api?.ModLoader.GetModSystem<TelephoneVoiceRoutingSystem>()?.ClearRoute(activeCallerPlayerUid);
                activeCallerPlayerUid = "";
            }
            MarkDirty(true);
        }

        private void SetState(TelephoneCallState newState, int durationMs)
        {
            callState = newState;
            stateUntilMs = durationMs > 0 && Api?.World != null
                ? Api.World.ElapsedMilliseconds + durationMs
                : 0;
        }

        public void ApplyServerComposeFlags(bool managedBySwitchboard, bool canCompose, string disabledReasonLangKey = null)
        {
            if (Api?.Side != EnumAppSide.Server) return;

            bool changed =
                composeManagedBySwitchboard != managedBySwitchboard ||
                composeEnabled != canCompose ||
                !string.Equals(composeDisabledReasonLangKey, disabledReasonLangKey ?? "Telegraph.Settings.DisabledNoPower", StringComparison.Ordinal);

            composeManagedBySwitchboard = managedBySwitchboard;
            composeEnabled = canCompose;
            composeDisabledReasonLangKey = disabledReasonLangKey ?? "Telegraph.Settings.DisabledNoPower";

            if (changed) MarkDirty(true);
        }

        public bool IsManagedBySwitchboard() => composeManagedBySwitchboard;
        public bool CanCompose() => composeEnabled;
        public string GetComposeDisabledReasonLangKey() => composeDisabledReasonLangKey ?? "Telegraph.Settings.DisabledNoPower";
        public string GetPhoneNumber() => phoneNumber ?? "";
        public string GetTargetNumber() => targetNumber ?? "";

        public string[] GetAvailableTargetNumbers()
        {
            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network == null) return Array.Empty<string>();

            return network.Nodes
                .OfType<BlockEntityTelephone>()
                .Where(t => t != null && !t.Pos.Equals(Pos))
                .Select(t => t.phoneNumber)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public bool IsTargetNumberBusy(string number)
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                return false;
            }

            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network == null)
            {
                return false;
            }

            var target = network.Nodes
                .OfType<BlockEntityTelephone>()
                .FirstOrDefault(t => !t.Pos.Equals(Pos) && string.Equals(t.phoneNumber, number, StringComparison.OrdinalIgnoreCase));

            return target != null && target.callState == TelephoneCallState.InCall;
        }

        public void SetPhoneNumber(string desired)
        {
            if (composeManagedBySwitchboard == false && Api?.Side == EnumAppSide.Server)
            {
                return;
            }

            if (composeManagedBySwitchboard && !composeEnabled && Api?.Side == EnumAppSide.Server)
            {
                return;
            }

            desired ??= "";
            desired = new string(desired.Where(char.IsDigit).Take(6).ToArray());

            if (composeManagedBySwitchboard && !string.IsNullOrWhiteSpace(desired))
            {
                var network = WireNetworkHandler.GetNetwork(NetworkUID);
                bool alreadyUsed = network?.Nodes
                    .OfType<BlockEntityTelephone>()
                    .Any(t => t != null && !t.Pos.Equals(Pos) && string.Equals(t.phoneNumber, desired, StringComparison.OrdinalIgnoreCase)) == true;
                if (alreadyUsed)
                {
                    return;
                }
            }

            phoneNumber = desired;
            if (!string.IsNullOrWhiteSpace(targetNumber))
            {
                var available = GetAvailableTargetNumbers();
                if (!available.Contains(targetNumber, StringComparer.OrdinalIgnoreCase))
                {
                    targetNumber = "";
                }
            }
            MarkDirty(true);
        }

        public void SetTargetNumber(string desired)
        {
            if (composeManagedBySwitchboard && !composeEnabled && Api?.Side == EnumAppSide.Server)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(desired))
            {
                targetNumber = "";
                MarkDirty(true);
                return;
            }

            targetNumber = desired.Trim();
            MarkDirty(true);
        }

        public void RequestSavePhoneNumber(string desired)
        {
            if (Api?.Side != EnumAppSide.Client) return;
            RPVoiceChatMod.TelephoneSettingsClientChannel?.SendPacket(new TelephoneSettingsPacket
            {
                TelephonePos = Pos,
                Operation = TelephoneSettingsOperation.SetNumber,
                Value = desired ?? ""
            });
        }

        public void RequestTargetNumberChange(string value)
        {
            if (Api?.Side != EnumAppSide.Client) return;
            RPVoiceChatMod.TelephoneSettingsClientChannel?.SendPacket(new TelephoneSettingsPacket
            {
                TelephonePos = Pos,
                Operation = TelephoneSettingsOperation.SetTarget,
                Value = value ?? ""
            });
        }

        public void RequestStartCall()
        {
            if (Api?.Side != EnumAppSide.Client) return;
            RPVoiceChatMod.TelephoneSettingsClientChannel?.SendPacket(new TelephoneSettingsPacket
            {
                TelephonePos = Pos,
                Operation = TelephoneSettingsOperation.StartCall,
                Value = ""
            });
        }

        public bool StartCall(IPlayer byPlayer)
        {
            if (Api?.Side != EnumAppSide.Server) return false;
            if (NetworkUID == 0) return false;
            if (callState != TelephoneCallState.Idle) return false;

            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network == null) return false;

            if (composeManagedBySwitchboard && !composeEnabled)
            {
                return false;
            }

            BEWireNode targetNode = null;
            if (composeManagedBySwitchboard)
            {
                if (string.IsNullOrWhiteSpace(targetNumber))
                {
                    return false;
                }

                targetNode = network.Nodes
                    .OfType<BlockEntityTelephone>()
                    .FirstOrDefault(t => !t.Pos.Equals(Pos) && string.Equals(t.phoneNumber, targetNumber, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                if (!TryResolveDirectPeerEndpoint(out targetNode, out _))
                {
                    return false;
                }
            }

            if (targetNode == null)
            {
                return false;
            }

            if (targetNode is BlockEntityTelephone targetTelephone && targetTelephone.callState != TelephoneCallState.Idle)
            {
                return false;
            }

            if (targetNode is BlockEntityTelephone targetPhone)
            {
                // Telephone <-> telephone: explicit answer required by the callee.
                if (string.IsNullOrWhiteSpace(byPlayer?.PlayerUID))
                {
                    return false;
                }
                targetPhone.RingFromNetwork(byPlayer);
                targetPhone.incomingCallerPlayerUid = byPlayer.PlayerUID;
                targetPhone.incomingCallerTelephonePos = Pos.Copy();
                SetState(TelephoneCallState.Ringing, RingDurationMs);
                activeCallerPlayerUid = byPlayer.PlayerUID;
                targetPhone.MarkDirty(true);
                MarkDirty(true);
                return true;
            }

            // Telephone <-> speaker (or other non-phone voice endpoint): auto-answer.
            SetState(TelephoneCallState.InCall, InCallDurationMs);
            if (!string.IsNullOrWhiteSpace(byPlayer?.PlayerUID))
            {
                activeCallerPlayerUid = byPlayer.PlayerUID;
                int emitRange = targetNode is ITelephoneVoiceEndpoint voiceEndpoint ? voiceEndpoint.VoiceEmissionRangeBlocks : 2;
                Vec3d emitPos = new Vec3d(targetNode.Pos.X + 0.5, targetNode.Pos.Y + 1.2, targetNode.Pos.Z + 0.5);
                Api?.ModLoader.GetModSystem<TelephoneVoiceRoutingSystem>()?.SetRoute(byPlayer.PlayerUID, emitPos, emitRange);
            }
            MarkDirty(true);
            return true;
        }

        private bool TryResolveDirectPeerEndpoint(out BEWireNode targetNode, out string failureLangKey)
        {
            targetNode = null;
            failureLangKey = null;

            // Generic direct-call mode:
            // one and only one other telephone-voice endpoint reachable from this node.
            // We traverse the physical wire graph so connectors/infrastructure are supported.
            var peers = GetReachableTelephoneVoiceEndpoints()
                .ToArray();

            if (peers.Length == 0)
            {
                failureLangKey = "Telephone.Call.Failed.NoPeer";
                return false;
            }

            if (peers.Length > 1)
            {
                failureLangKey = "Telephone.Call.Failed.NotUniquePeer";
                return false;
            }

            if (peers[0] is BlockEntityTelephone peerTelephone && peerTelephone.callState != TelephoneCallState.Idle)
            {
                failureLangKey = "Telephone.Call.Failed.TargetBusy";
                return false;
            }

            targetNode = peers[0];
            return true;
        }

        private System.Collections.Generic.IEnumerable<BEWireNode> GetReachableTelephoneVoiceEndpoints()
        {
            var reachable = WireNetworkHandler.GetReachableNodes(this);
            return reachable.Where(n => n != null && !ReferenceEquals(n, this) && n is ITelephoneVoiceEndpoint);
        }

        public string GetCallFailureLangKeyForUi()
        {
            if (NetworkUID == 0)
            {
                return "Telephone.Call.Failed.NoNetwork";
            }

            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network == null)
            {
                return "Telephone.Call.Failed.NoNetwork";
            }

            if (composeManagedBySwitchboard)
            {
                if (!composeEnabled)
                {
                    return GetComposeDisabledReasonLangKey();
                }

                if (string.IsNullOrWhiteSpace(targetNumber))
                {
                    return "Telephone.Call.Failed.NoTarget";
                }

                var targetTelephone = network.Nodes
                    .OfType<BlockEntityTelephone>()
                    .FirstOrDefault(t => !t.Pos.Equals(Pos) && string.Equals(t.phoneNumber, targetNumber, StringComparison.OrdinalIgnoreCase));

                if (targetTelephone == null)
                {
                    return "Telephone.Call.Failed.TargetUnavailable";
                }

                if (targetTelephone.callState != TelephoneCallState.Idle)
                {
                    return "Telephone.Call.Failed.TargetBusy";
                }

                return null;
            }

            if (!TryResolveDirectPeerEndpoint(out _, out string directModeFailure))
            {
                return directModeFailure;
            }

            return null;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                dsc.AppendLine(UIUtils.I18n("Telephone.Number", phoneNumber));
            }
            if (composeManagedBySwitchboard && !composeEnabled)
            {
                dsc.AppendLine(UIUtils.I18n(GetComposeDisabledReasonLangKey()));
            }
            dsc.AppendLine(UIUtils.I18n("Telephone.State", UIUtils.I18n($"Telephone.State.{callState}")));
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (!string.IsNullOrWhiteSpace(activeCallerPlayerUid))
            {
                Api?.ModLoader.GetModSystem<TelephoneVoiceRoutingSystem>()?.ClearRoute(activeCallerPlayerUid);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (!string.IsNullOrWhiteSpace(activeCallerPlayerUid))
            {
                Api?.ModLoader.GetModSystem<TelephoneVoiceRoutingSystem>()?.ClearRoute(activeCallerPlayerUid);
            }
        }

        private bool TryAcceptIncomingCall(IPlayer byPlayer)
        {
            if (Api?.Side != EnumAppSide.Server) return false;
            if (callState != TelephoneCallState.Ringing) return false;
            if (incomingCallerTelephonePos == null) return false;

            var callerTelephone = Api.World.BlockAccessor.GetBlockEntity(incomingCallerTelephonePos) as BlockEntityTelephone;
            if (callerTelephone == null || callerTelephone.callState != TelephoneCallState.Ringing)
            {
                incomingCallerPlayerUid = "";
                incomingCallerTelephonePos = null;
                MarkDirty(true);
                return false;
            }

            SetState(TelephoneCallState.InCall, InCallDurationMs);
            callerTelephone.SetState(TelephoneCallState.InCall, InCallDurationMs);

            if (!string.IsNullOrWhiteSpace(incomingCallerPlayerUid))
            {
                Vec3d emitPosForCaller = new Vec3d(Pos.X + 0.5, Pos.Y + 1.2, Pos.Z + 0.5);
                Api?.ModLoader.GetModSystem<TelephoneVoiceRoutingSystem>()?.SetRoute(
                    incomingCallerPlayerUid,
                    emitPosForCaller,
                    VoiceEmissionRangeBlocks
                );
            }

            if (!string.IsNullOrWhiteSpace(byPlayer?.PlayerUID))
            {
                activeCallerPlayerUid = byPlayer.PlayerUID;
                Vec3d emitPosForCallee = new Vec3d(callerTelephone.Pos.X + 0.5, callerTelephone.Pos.Y + 1.2, callerTelephone.Pos.Z + 0.5);
                Api?.ModLoader.GetModSystem<TelephoneVoiceRoutingSystem>()?.SetRoute(
                    byPlayer.PlayerUID,
                    emitPosForCallee,
                    callerTelephone.VoiceEmissionRangeBlocks
                );
            }

            incomingCallerPlayerUid = "";
            incomingCallerTelephonePos = null;
            callerTelephone.MarkDirty(true);
            MarkDirty(true);
            return true;
        }
    }
}
