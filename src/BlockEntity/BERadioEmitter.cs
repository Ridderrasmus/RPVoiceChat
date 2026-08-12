using System.Linq;
using System;
using System.Text;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.Gui;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioEmitter : BEWireNode, IWireTypedNode
    {
        private RadioEmitterDialog dialog;
        private RadioEmitterOperatingMode operatingMode = RadioEmitterOperatingMode.WiredSource;
        private string repeaterFrequency = "100.0";
        private int lastSyncedPowerPercent = -1;
        private bool wasTransmitting;
        private RotatingMechPartRenderer mechPartRenderer;

        public override bool IsActiveEndpoint => true;
        protected override int MaxConnections => 1;
        public WireNodeKind WireNodeKind => WireNodeKind.RadioEmitter;
        public float PowerPercent { get; private set; }
        public RadioEmitterOperatingMode OperatingMode => operatingMode;
        public bool IsRepeaterMode => operatingMode == RadioEmitterOperatingMode.Repeater;
        public string RepeaterFrequency => repeaterFrequency ?? "";
        public bool IsWirelessTransmitting => HasSufficientTransmitPower() && !IsRepeaterMode;
        public bool IsRepeaterRelaying => HasSufficientTransmitPower() && IsRepeaterMode && CanRelayRepeaterFrequency();

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            DisableConsumerInstancedRenderer();

            if (api.Side == EnumAppSide.Server)
            {
                (api as ICoreServerAPI)?.Event.RegisterGameTickListener(OnServerTick, 100);
                TryDiscoverNetwork();
                RadioBlockIndex.RegisterEmitter(Pos);
            }
            else if (api is ICoreClientAPI capi)
            {
                mechPartRenderer = new RotatingMechPartRenderer(
                    this,
                    capi,
                    new AssetLocation("rpvoicechat:shapes/block/radioemitter/radioemitter_mechpart.json"),
                    GetMechPartBaseRotY());
            }
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                return true;
            }

            if (Api is not ICoreClientAPI capi)
            {
                return true;
            }

            if (dialog?.IsOpened() == true)
            {
                return true;
            }

            dialog = new RadioEmitterDialog(capi, this);
            dialog.TryOpen();
            return true;
        }

        public void RequestSetOperatingMode(RadioEmitterOperatingMode mode)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetEmitterMode,
                IntValue = (int)mode
            });
        }

        public void SetOperatingMode(RadioEmitterOperatingMode mode)
        {
            bool leavingRepeater = operatingMode == RadioEmitterOperatingMode.Repeater
                && mode == RadioEmitterOperatingMode.WiredSource;

            // Keep existing wires when entering repeater mode: they stay dormant for RF
            // (CollectWiredTransmissionPoints skips repeaters) and become active again
            // when returning to wired source. New wire connections remain denied while repeating.
            operatingMode = mode;
            MarkDirty();
            dialog?.RefreshData();

            if (leavingRepeater)
            {
                RejoinWireNetworkAfterLeavingRepeater();
            }

            SyncWirelessRegistration();
        }

        public bool HasSufficientTransmitPower()
        {
            float minPercent = ServerConfigManager.RadioNetworkMinPowerPercent / 100f;
            return PowerPercent >= minPercent;
        }

        public int GetEffectiveTransmitRangeBlocks()
        {
            int bonus = CountAntennaPartsAbove() * ServerConfigManager.RadioAntennaPartRangeBonusBlocks;
            return ServerConfigManager.RadioEmitterBaseRangeBlocks + bonus;
        }

        public int CountAntennaPartsAbove()
        {
            if (Api?.World?.BlockAccessor == null)
            {
                return 0;
            }

            int count = 0;
            BlockPos scan = Pos.UpCopy();
            while (true)
            {
                var part = Api.World.BlockAccessor.GetBlockEntity(scan) as BlockEntityRadioAntennaPart;
                if (part == null)
                {
                    break;
                }

                count++;
                scan = scan.UpCopy();
            }

            return count;
        }

        /// <summary>
        /// Called when antenna segments above this emitter are added/removed so RF range + open GUI stay in sync.
        /// </summary>
        public void OnAntennaStackChanged()
        {
            dialog?.RefreshData();
            if (Api?.Side == EnumAppSide.Server)
            {
                MarkDirty();
            }
        }

        public string GetConsoleFrequency()
        {
            return RadioWireNetworkHelper.FindSupervisionConsole(this)?.Frequency ?? "";
        }

        public string GetConsoleDisplayName()
        {
            return RadioWireNetworkHelper.FindSupervisionConsole(this)?.DisplayName ?? "";
        }

        public void RequestSetRepeaterFrequency(string desired)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetRepeaterFrequency,
                Value = desired ?? ""
            });
        }

        /// <returns>False when the frequency is already claimed by another transmitter.</returns>
        public bool TrySetRepeaterFrequency(string desired)
        {
            string normalized = (desired ?? "").Trim();
            if (RadioFrequencyUtil.Matches(normalized, repeaterFrequency))
            {
                return true;
            }

            if (Api?.World != null && !RadioTransmitFrequencyGuard.IsFrequencyAvailable(Api.World, normalized, Pos))
            {
                return false;
            }

            repeaterFrequency = normalized;
            MarkDirty();
            dialog?.RefreshData();
            SyncWirelessRegistration();
            return true;
        }

        public bool CanRelayRepeaterFrequency()
        {
            if (!IsRepeaterMode || Api?.Side != EnumAppSide.Server)
            {
                return false;
            }

            string frequency = RadioFrequencyUtil.Normalize(repeaterFrequency);
            if (frequency.Length == 0)
            {
                return false;
            }

            var points = RadioRfTransmissionService.CollectWiredTransmissionPoints(Api.World.Api as ICoreServerAPI);
            Vec3d repeaterPos = Pos.ToVec3d().Add(0.5, 0.5, 0.5);
            return points.Any(point =>
                point.Dimension == Pos.dimension
                && RadioFrequencyUtil.Matches(point.Frequency, frequency)
                && repeaterPos.DistanceTo(point.Position) <= point.RangeBlocks);
        }

        public string GetActiveRfFrequency()
        {
            if (IsRepeaterMode)
            {
                return RepeaterFrequency;
            }

            return GetConsoleFrequency();
        }

        private void OnServerTick(float dt)
        {
            var consumer = GetBehavior<BEBehaviorMPConsumer>();
            float speed = consumer?.TrueSpeed ?? 0f;
            PowerPercent = Math.Max(0f, Math.Min(1f, speed));

            int syncedPercent = (int)Math.Round(PowerPercent * 100);
            bool transmitting = IsWirelessTransmitting;
            if (syncedPercent != lastSyncedPowerPercent || transmitting != wasTransmitting)
            {
                lastSyncedPowerPercent = syncedPercent;
                wasTransmitting = transmitting;
                MarkDirty();
            }

            SyncWirelessRegistration();
        }

        private void SyncWirelessRegistration()
        {
            if (Api?.Side != EnumAppSide.Server)
            {
                return;
            }

            long networkId = NetworkUID;
            bool shouldRegister = (IsWirelessTransmitting || IsRepeaterRelaying) && networkId != 0;
            if (!shouldRegister)
            {
                WirelessTopologyRegistry.UnregisterAntenna(Pos);
                return;
            }

            WirelessTopologyRegistry.RegisterAntenna(Pos, networkId);
        }

        public void TryDiscoverNetwork()
        {
            if (Api?.Side != EnumAppSide.Server || Block?.Variant == null)
            {
                return;
            }

            if (!Block.Variant.TryGetValue("side", out string sideStr))
            {
                return;
            }

            BlockFacing connectorFace = BlockFacing.FromCode(sideStr);
            if (connectorFace == null)
            {
                return;
            }

            GetBehavior<BEBehaviorMPBase>()?.CreateJoinAndDiscoverNetwork(connectorFace);
        }

        /// <summary>
        /// After leaving repeater mode, adopt a neighbor's wire network if we still have
        /// cable links but lost <see cref="NetworkUID"/> (or were dropped from the network set).
        /// </summary>
        private void RejoinWireNetworkAfterLeavingRepeater()
        {
            if (Api?.Side != EnumAppSide.Server || GetConnections().Count == 0)
            {
                return;
            }

            foreach (var connection in GetConnections())
            {
                BEWireNode other = connection.GetOtherNode(this);
                if (other == null || other.NetworkUID == 0)
                {
                    continue;
                }

                var network = WireNetworkHandler.GetNetwork(other.NetworkUID);
                if (network == null)
                {
                    continue;
                }

                network.AddNode(this);
                NetworkUID = other.NetworkUID;
                WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(this, network);
                WireNetworkHandler.RebuildNetworkState(NetworkUID);
                if (Api is ICoreServerAPI sapi)
                {
                    WireTopologyConnectivity.NotifyNode(sapi, this);
                }

                return;
            }

            if (NetworkUID != 0)
            {
                WireNetworkHandler.RebuildNetworkState(NetworkUID);
                if (Api is ICoreServerAPI sapi)
                {
                    WireTopologyConnectivity.NotifyNode(sapi, this);
                }
            }
        }

        private float GetMechPartBaseRotY()
        {
            // Same orientation scheme as switchboard (shape + axle hole authored the same way).
            if (Block?.Variant != null && Block.Variant.TryGetValue("side", out string sideStr))
            {
                return sideStr switch
                {
                    "north" => 0f,
                    "east" => 270f,
                    "west" => 90f,
                    "south" => 180f,
                    _ => 0f
                };
            }

            return 0f;
        }

        private void DisableConsumerInstancedRenderer()
        {
            var consumer = GetBehavior<BEBehaviorMPConsumer>();
            if (consumer == null)
            {
                return;
            }

            consumer.Shape = null;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            operatingMode = (RadioEmitterOperatingMode)tree.GetInt("rpvc:radioEmitterMode", (int)operatingMode);
            repeaterFrequency = tree.GetString("rpvc:radioRepeaterFrequency", repeaterFrequency);
            PowerPercent = tree.GetFloat("rpvc:radioEmitterPowerPercent", PowerPercent);
            DisableConsumerInstancedRenderer();
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("rpvc:radioEmitterMode", (int)operatingMode);
            tree.SetString("rpvc:radioRepeaterFrequency", repeaterFrequency ?? "");
            tree.SetFloat("rpvc:radioEmitterPowerPercent", PowerPercent);
        }

        /// <summary>
        /// Quern/switchboard-style: tessellate the static cabinet explicitly and skip default aggregation
        /// so MPConsumer cannot steal or double the mesh.
        /// </summary>
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Block == null)
            {
                return false;
            }

            CompositeShape blockShape = Block.Shape;
            if (blockShape?.Base == null)
            {
                return false;
            }

            AssetLocation shapeLoc = blockShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = Shape.TryGet(Api, shapeLoc);
            if (shape == null)
            {
                return false;
            }

            tesselator.TesselateShape(
                Block,
                shape,
                out MeshData mesh,
                new Vec3f(blockShape.rotateX, blockShape.rotateY, blockShape.rotateZ),
                blockShape.QuantityElements,
                blockShape.SelectiveElements);

            mesher.AddMeshData(mesh);
            return true;
        }

        public override void OnBlockRemoved()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                WirelessTopologyRegistry.UnregisterAntenna(Pos);
                RadioBlockIndex.UnregisterEmitter(Pos);
            }

            mechPartRenderer?.Dispose();
            mechPartRenderer = null;
            base.OnBlockRemoved();
            dialog?.TryClose();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            int power = (int)System.Math.Round(PowerPercent * 100);
            int minPower = ServerConfigManager.RadioNetworkMinPowerPercent;
            dsc.AppendLine(UIUtils.I18n("Radio.Emitter.Info.Power", power, minPower));
            dsc.AppendLine(UIUtils.I18n("Radio.Emitter.Info.Range", GetEffectiveTransmitRangeBlocks()));
            if (!HasSufficientTransmitPower())
            {
                dsc.AppendLine(UIUtils.I18n("Radio.Emitter.Gui.InsufficientPower", minPower));
            }
        }

        public override void OnBlockUnloaded()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                WirelessTopologyRegistry.UnregisterAntenna(Pos);
                RadioBlockIndex.UnregisterEmitter(Pos);
            }

            mechPartRenderer?.Dispose();
            mechPartRenderer = null;
            base.OnBlockUnloaded();
        }
    }
}
