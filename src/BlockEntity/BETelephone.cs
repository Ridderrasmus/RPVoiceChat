using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using RPVoiceChat.Config;
using RPVoiceChat.Gui;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityTelephone : BEWireNode, INetworkRoot, IWireTypedNode, ITelephoneVoiceEndpoint
    {
        private const string TelephoneShapeCode = "block/telephone";
        private const string InCallAnimationCode = "playing-sound";
        private static readonly Vec3f CoilWireOffsetNorth = new Vec3f(11.9f / 16f, 5.485f / 16f, 4.175f / 16f);

        private enum TelephoneCallState
        {
            Idle,
            Ringing,
            InCall
        }

        // Auto-hangup timeout while waiting for answer.
        private const int RingDurationMs = 20000;
        private const int IncomingRingRepeatMs = 2000;
        private const int RingbackRepeatMs = 2000;
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
        private BlockPos activePeerTelephonePos;
        private long nextRingingSoundAtMs;
        private long originalCreatedNetworkID = 0;
        private Vintagestory.GameContent.BEBehaviorAnimatable Animatable => GetBehavior<Vintagestory.GameContent.BEBehaviorAnimatable>();
        private BlockEntityAnimationUtil AnimUtil => Animatable?.animUtil;

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

            if (api.Side == EnumAppSide.Client)
            {
                InitializeClientAnimator();
                SyncInCallAnimationState();
            }

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

        protected override void SetWireAttachmentOffset()
        {
            WireAttachmentOffset = RotateLocalOffsetByBlockSide(CoilWireOffsetNorth);
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                // Telephone calls are explicitly controlled from the GUI actions.
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
            TelephoneCallState previousState = callState;
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
            int peerX = tree.GetInt("rpvc:telephonePeerPosX", int.MinValue);
            int peerY = tree.GetInt("rpvc:telephonePeerPosY", int.MinValue);
            int peerZ = tree.GetInt("rpvc:telephonePeerPosZ", int.MinValue);
            activePeerTelephonePos = peerX == int.MinValue ? null : new BlockPos(peerX, peerY, peerZ);
            long savedOriginalCreatedNetworkID = tree.GetLong("rpvc:telephoneOriginalCreatedNetworkID", 0);
            if (savedOriginalCreatedNetworkID != 0)
            {
                originalCreatedNetworkID = savedOriginalCreatedNetworkID;
            }
            if (worldForResolving.Side == EnumAppSide.Client)
            {
                SyncInCallAnimationState();
                if (previousState != callState)
                {
                    MarkDirty(true);
                }
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
            if (activePeerTelephonePos != null)
            {
                tree.SetInt("rpvc:telephonePeerPosX", activePeerTelephonePos.X);
                tree.SetInt("rpvc:telephonePeerPosY", activePeerTelephonePos.Y);
                tree.SetInt("rpvc:telephonePeerPosZ", activePeerTelephonePos.Z);
            }
            else
            {
                tree.SetInt("rpvc:telephonePeerPosX", int.MinValue);
                tree.SetInt("rpvc:telephonePeerPosY", int.MinValue);
                tree.SetInt("rpvc:telephonePeerPosZ", int.MinValue);
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
            Api.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/block/callbell/callbell_1.ogg"),
                Pos.X + 0.5,
                Pos.Y + 0.5,
                Pos.Z + 0.5,
                sourcePlayer,
                false,
                12f,
                0.6f
            );
            // Prevent immediate overlap on next server tick after initial ring.
            nextRingingSoundAtMs = Api.World.ElapsedMilliseconds + IncomingRingRepeatMs;

            MarkDirty(true);
        }

        private void OnServerTick(float _dt)
        {
            if (callState == TelephoneCallState.Idle || Api?.World == null) return;

            if (callState == TelephoneCallState.Ringing)
            {
                TryPlayLoopingRingingSounds();
            }

            if (stateUntilMs <= 0) return;
            if (Api.World.ElapsedMilliseconds < stateUntilMs) return;

            EndCall();
        }

        private void SetState(TelephoneCallState newState, int durationMs)
        {
            callState = newState;
            stateUntilMs = durationMs > 0 && Api?.World != null
                ? Api.World.ElapsedMilliseconds + durationMs
                : 0;
            nextRingingSoundAtMs = 0;
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
        public bool IsInCall() => callState == TelephoneCallState.InCall;
        public bool IsWaitingForAnswer() => callState == TelephoneCallState.Ringing && incomingCallerTelephonePos == null;
        public bool HasIncomingCall() => callState == TelephoneCallState.Ringing && incomingCallerTelephonePos != null;
        public bool IsCallSessionActive() => callState != TelephoneCallState.Idle;

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

        public void RequestEndCall()
        {
            if (Api?.Side != EnumAppSide.Client) return;
            RPVoiceChatMod.TelephoneSettingsClientChannel?.SendPacket(new TelephoneSettingsPacket
            {
                TelephonePos = Pos,
                Operation = TelephoneSettingsOperation.EndCall,
                Value = ""
            });
        }

        public bool StartCall(IPlayer byPlayer)
        {
            if (Api?.Side != EnumAppSide.Server) return false;
            if (NetworkUID == 0) return false;
            if (HasIncomingCall())
            {
                return TryAcceptIncomingCall(byPlayer);
            }
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
                targetPhone.activePeerTelephonePos = Pos.Copy();
                activePeerTelephonePos = targetPhone.Pos.Copy();
                SetState(TelephoneCallState.Ringing, RingDurationMs);
                activeCallerPlayerUid = byPlayer.PlayerUID;
                targetPhone.MarkDirty(true);
                MarkDirty(true);
                return true;
            }

            // Telephone <-> speaker (or other non-phone voice endpoint): auto-answer.
            SetState(TelephoneCallState.InCall, 0);
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

        public void EndCall()
        {
            if (Api?.Side != EnumAppSide.Server)
            {
                return;
            }

            EndCallInternal(true);
        }

        private void EndCallInternal(bool notifyPeer)
        {
            if (callState == TelephoneCallState.Idle && string.IsNullOrWhiteSpace(activeCallerPlayerUid) && incomingCallerTelephonePos == null)
            {
                return;
            }

            BlockPos peerPos = activePeerTelephonePos ?? incomingCallerTelephonePos;
            if (!string.IsNullOrWhiteSpace(activeCallerPlayerUid))
            {
                Api?.ModLoader.GetModSystem<TelephoneVoiceRoutingSystem>()?.ClearRoute(activeCallerPlayerUid);
            }

            SetState(TelephoneCallState.Idle, 0);
            activeCallerPlayerUid = "";
            incomingCallerPlayerUid = "";
            incomingCallerTelephonePos = null;
            activePeerTelephonePos = null;
            MarkDirty(true);

            if (!notifyPeer || peerPos == null)
            {
                return;
            }

            var peerTelephone = Api?.World?.BlockAccessor?.GetBlockEntity(peerPos) as BlockEntityTelephone;
            peerTelephone?.EndCallInternal(false);
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

        private void TryPlayLoopingRingingSounds()
        {
            if (Api?.World == null || callState != TelephoneCallState.Ringing)
            {
                return;
            }

            long now = Api.World.ElapsedMilliseconds;
            if (nextRingingSoundAtMs > now)
            {
                return;
            }

            if (HasIncomingCall())
            {
                Api.World.PlaySoundAt(
                    new AssetLocation(RPVoiceChatMod.modID, "sounds/block/callbell/callbell_1.ogg"),
                    Pos.X + 0.5,
                    Pos.Y + 0.5,
                    Pos.Z + 0.5,
                    null,
                    false,
                    12f,
                    0.6f
                );
                nextRingingSoundAtMs = now + IncomingRingRepeatMs;
                return;
            }

            if (IsWaitingForAnswer())
            {
                Api.World.PlaySoundAt(
                    new AssetLocation(RPVoiceChatMod.modID, "sounds/block/telephone/ringback-tone.ogg"),
                    Pos.X + 0.5,
                    Pos.Y + 0.5,
                    Pos.Z + 0.5,
                    null,
                    false,
                    2f,
                    0.6f
                );
                nextRingingSoundAtMs = now + RingbackRepeatMs;
            }
        }

        private void SyncInCallAnimationState()
        {
            if (Api?.Side == EnumAppSide.Client)
            {
                InitializeClientAnimator();
            }

            var animUtil = AnimUtil;
            if (animUtil == null) return;

            if (callState == TelephoneCallState.InCall)
            {
                StartAnimationIfNotRunning(InCallAnimationCode);
            }
            else
            {
                StopAnimation(InCallAnimationCode);
            }
        }

        private void InitializeClientAnimator()
        {
            var animUtil = AnimUtil;
            if (animUtil == null || animUtil.animator != null || Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            string shapePath = ResolveShapePath();
            if (Block?.Code != null && !string.IsNullOrWhiteSpace(shapePath))
            {
                var assetLoc = new AssetLocation(Block.Code.Domain, "shapes/" + shapePath + ".json");
                var shape = Shape.TryGet(Api, assetLoc);
                if (shape?.Animations != null && shape.Animations.Length > 0)
                {
                    shape.InitForAnimations(Api.Logger, shapePath, Array.Empty<string>());
                }
            }

            float rotYDeg = GetBlockSideRotY();
            animUtil.InitializeAnimator(shapePath, null, null, new Vec3f(0, rotYDeg, 0));
        }

        private string ResolveShapePath()
        {
            return Block?.Shape?.Base?.Path ?? TelephoneShapeCode;
        }

        private void StartAnimationIfNotRunning(string animationCode)
        {
            var animUtil = AnimUtil;
            if (animUtil == null) return;
            if (animUtil.activeAnimationsByAnimCode.ContainsKey(animationCode)) return;

            animUtil.StartAnimation(new AnimationMetaData
            {
                Animation = animationCode,
                Code = animationCode
            });
        }

        private void StopAnimation(string animationCode)
        {
            var animUtil = AnimUtil;
            if (animUtil == null) return;
            animUtil.StopAnimation(animationCode);
        }

        private float GetBlockSideRotY()
        {
            return Block?.Variant?.TryGetValue("side", out string side) == true
                ? side switch
                {
                    "north" => 0f,
                    "east" => 270f,
                    "west" => 90f,
                    "south" => 180f,
                    _ => 0f
                }
                : 0f;
        }

        private static Vec3f RotateAroundCenter(Vec3f point, float rotDeg)
        {
            if (Math.Abs(rotDeg) < 0.001f) return point;

            float rad = rotDeg * GameMath.DEG2RAD;
            float cos = GameMath.Cos(rad);
            float sin = GameMath.Sin(rad);

            float dx = point.X - 0.5f;
            float dz = point.Z - 0.5f;

            // Match in-game rotation handedness used by rotateYByType.
            float x = dx * cos + dz * sin;
            float z = -dx * sin + dz * cos;

            return new Vec3f(x + 0.5f, point.Y, z + 0.5f);
        }

        private Vec3f RotateLocalOffsetByBlockSide(Vec3f offsetNorth)
        {
            return RotateAroundCenter(offsetNorth, GetBlockSideRotY());
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
            EndCall();
            base.OnBlockRemoved();
            if (!string.IsNullOrWhiteSpace(activeCallerPlayerUid))
            {
                Api?.ModLoader.GetModSystem<TelephoneVoiceRoutingSystem>()?.ClearRoute(activeCallerPlayerUid);
            }
        }

        public override void OnBlockUnloaded()
        {
            EndCall();
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

            SetState(TelephoneCallState.InCall, 0);
            callerTelephone.SetState(TelephoneCallState.InCall, 0);
            activePeerTelephonePos = callerTelephone.Pos.Copy();
            callerTelephone.activePeerTelephonePos = Pos.Copy();

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
