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
                var activeEndpoints = network.Nodes.Where(n => n.IsActiveEndpoint && !n.Pos.Equals(Pos)).ToArray();
                if (activeEndpoints.Length != 1) return false;
                targetNode = activeEndpoints[0];
            }

            if (targetNode == null)
            {
                return false;
            }

            if (targetNode is BlockEntityTelephone targetTelephone && targetTelephone.callState == TelephoneCallState.InCall)
            {
                return false;
            }

            SetState(TelephoneCallState.InCall, InCallDurationMs);
            if (targetNode is BlockEntityTelephone targetPhone)
            {
                targetPhone.RingFromNetwork(byPlayer);
                targetPhone.SetState(TelephoneCallState.InCall, InCallDurationMs);
                targetPhone.MarkDirty(true);
            }

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

                if (targetTelephone.callState == TelephoneCallState.InCall)
                {
                    return "Telephone.Call.Failed.TargetBusy";
                }

                return null;
            }

            var activeEndpoints = network.Nodes.Where(n => n.IsActiveEndpoint && !n.Pos.Equals(Pos)).ToArray();
            if (activeEndpoints.Length == 0)
            {
                return "Telephone.Call.Failed.NoPeer";
            }

            if (activeEndpoints.Length > 1)
            {
                return "Telephone.Call.Failed.NotUniquePeer";
            }

            if (activeEndpoints[0] is BlockEntityTelephone peerTelephone && peerTelephone.callState == TelephoneCallState.InCall)
            {
                return "Telephone.Call.Failed.TargetBusy";
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
    }
}
