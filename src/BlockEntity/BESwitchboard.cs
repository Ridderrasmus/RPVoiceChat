using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.GameContent.Renderers;

using RPVoiceChat.Gui;

using RPVoiceChat.Networking.Packets;

using RPVoiceChat.Systems;

using RPVoiceChat.Util;

using System;

using System.Collections.Generic;

using System.Linq;

using System.Text;

using Vintagestory.API.Client;

using Vintagestory.API.Common;

using Vintagestory.API.Datastructures;

using Vintagestory.API.MathTools;

using Vintagestory.API.Server;

using Vintagestory.GameContent.Mechanics;



namespace RPVoiceChat.GameContent.BlockEntity

{

    public class BlockEntitySwitchboard : BEWireNode, IWireTypedNode, ISwitchboardNode

    {

        private GuiDialogSwitchboard dialog;



        /// <summary>

        /// Last known custom network name for this block (mirrors <see cref="WireNetwork.CustomName"/> when loaded).

        /// Persisted so the label survives chunk reload / server restart; the in-memory <see cref="WireNetwork"/> is not saved.

        /// </summary>

        private string persistedNetworkCustomName = "";



        private bool networkNameApplyPending;
        private bool usePowerRequirements = true;

        /// <summary>BellHammer-style: only <see cref="MarkDirty"/> when the displayed integer % changes, so clients receive updates via tree sync.</summary>
        private int _lastSyncedPowerPercent = -1;

        /// <summary>Detects crossing the configured min-power threshold when the integer % is unchanged.</summary>
        private bool _lastAboveMinPower;
        private RotatingMechPartRenderer _mechPartRenderer;



        public WireNodeKind WireNodeKind => WireNodeKind.Switchboard;

        public float PowerPercent { get; private set; }
        public bool UsePowerRequirements => usePowerRequirements;



        protected override int MaxConnections => 4;
        private static readonly Vec3f[] AxisOffsetsNorth =
        {
            // Axis1..4 centers computed from the shape with their own local 45deg rotation.
            ComputeRotatedAxisCenter(4.5f, 5.5f, 3.0f, 4.0f, 9.5f, 6.0f, 45f),
            ComputeRotatedAxisCenter(11.5f, 12.5f, 3.0f, 4.0f, 16.5f, 6.0f, 45f),
            ComputeRotatedAxisCenter(4.5f, 5.5f, -1.0f, 0.0f, 9.5f, 2.0f, 45f),
            ComputeRotatedAxisCenter(11.5f, 12.5f, -1.0f, 0.0f, 16.5f, 2.0f, 45f)
        };



        public override void Initialize(ICoreAPI api)

        {

            base.Initialize(api);
            DisableConsumerInstancedRenderer();

            if (api.Side == EnumAppSide.Server)

            {

                (api as ICoreServerAPI)?.Event.RegisterGameTickListener(OnServerSwitchboardTick, 100);

                TryDiscoverNetwork();

            }

            else

            {
                if (api is ICoreClientAPI capi)
                {
                    _mechPartRenderer = new RotatingMechPartRenderer(
                        this,
                        capi,
                        new AssetLocation("rpvoicechat:shapes/block/switchboard/switchboard_mechpart.json"),
                        GetMechPartBaseRotY()
                    );
                }

                RegisterGameTickListener(_ => TryApplyPersistedNetworkName(), 1000);

            }

        }

        public override Vec3f GetWireAttachmentOffsetFor(BlockPos otherNodePos)
        {
            if (otherNodePos == null) return base.GetWireAttachmentOffsetFor(otherNodePos);

            Vec3f[] axisOffsetsCurrent = AxisOffsetsNorth
                .Select(RotateLocalOffsetByBlockSide)
                .ToArray();

            var entries = GetConnections()
                .Select(c => c.GetOtherBlockPos(Pos))
                .Where(p => p != null)
                .Select(p => (
                    Pos: p,
                    LocalCurrent: new Vec3f(
                        p.X - Pos.X + 0.5f,
                        0.5f,
                        p.Z - Pos.Z + 0.5f
                    )
                ))
                .OrderBy(entry => entry.LocalCurrent.X)
                .ThenBy(entry => entry.LocalCurrent.Z)
                .ThenBy(entry => entry.Pos.X)
                .ThenBy(entry => entry.Pos.Y)
                .ThenBy(entry => entry.Pos.Z)
                .ToList();

            if (entries.Count == 0) return base.GetWireAttachmentOffsetFor(otherNodePos);

            var assignedAxisByNode = AssignAxesByBestGlobalDistance(entries, axisOffsetsCurrent);

            if (!assignedAxisByNode.TryGetValue(GetPosKey(otherNodePos), out int index))
            {
                return base.GetWireAttachmentOffsetFor(otherNodePos);
            }

            index = GameMath.Clamp(index, 0, AxisOffsetsNorth.Length - 1);

            return axisOffsetsCurrent[index];
        }

        private static (int X, int Y, int Z) GetPosKey(BlockPos pos)
        {
            return (pos.X, pos.Y, pos.Z);
        }

        private static float DistanceSqXZ(Vec3f a, Vec3f b)
        {
            // Cost metric used by the axis assignment solver (minimum total XZ distance).
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private Dictionary<(int X, int Y, int Z), int> AssignAxesByBestGlobalDistance(
            IReadOnlyList<(BlockPos Pos, Vec3f LocalCurrent)> entries,
            IReadOnlyList<Vec3f> axisOffsetsCurrent)
        {
            int count = Math.Min(entries.Count, axisOffsetsCurrent.Count);
            var assignedAxisByNode = new Dictionary<(int X, int Y, int Z), int>();
            if (count == 0) return assignedAxisByNode;

            var usedAxes = new bool[axisOffsetsCurrent.Count];
            var current = new int[count];
            var best = new int[count];
            Array.Fill(best, -1);
            float bestCost = float.MaxValue;

            void Search(int depth, float cost)
            {
                if (depth == count)
                {
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        Array.Copy(current, best, count);
                    }
                    return;
                }

                for (int axisIndex = 0; axisIndex < axisOffsetsCurrent.Count; axisIndex++)
                {
                    if (usedAxes[axisIndex]) continue;

                    float nextCost = cost + DistanceSqXZ(entries[depth].LocalCurrent, axisOffsetsCurrent[axisIndex]);
                    if (nextCost >= bestCost) continue;

                    usedAxes[axisIndex] = true;
                    current[depth] = axisIndex;
                    Search(depth + 1, nextCost);
                    usedAxes[axisIndex] = false;
                }
            }

            Search(0, 0f);

            for (int i = 0; i < count; i++)
            {
                int axisIndex = best[i];
                if (axisIndex < 0) continue;
                assignedAxisByNode[GetPosKey(entries[i].Pos)] = axisIndex;
            }

            return assignedAxisByNode;
        }

        private static Vec3f ComputeRotatedAxisCenter(
            float fromX, float toX,
            float fromZ, float toZ,
            float rotationOriginX, float rotationOriginZ,
            float rotYDeg)
        {
            float cx = (fromX + toX) * 0.5f / 16f;
            float cz = (fromZ + toZ) * 0.5f / 16f;
            var center = new Vec3f(cx, 14.625f / 16f, cz);
            var origin = new Vec3f(rotationOriginX / 16f, center.Y, rotationOriginZ / 16f);
            return RotateAroundPointXZ(center, origin, rotYDeg);
        }

        private static Vec3f RotateAroundPointXZ(Vec3f point, Vec3f origin, float rotDeg)
        {
            if (Math.Abs(rotDeg) < 0.001f) return point;

            float rad = rotDeg * GameMath.DEG2RAD;
            float cos = GameMath.Cos(rad);
            float sin = GameMath.Sin(rad);

            float dx = point.X - origin.X;
            float dz = point.Z - origin.Z;

            // Vintage Story Y-rotation behaves as clockwise in this local X/Z frame.
            float x = dx * cos + dz * sin;
            float z = -dx * sin + dz * cos;

            return new Vec3f(x + origin.X, point.Y, z + origin.Z);
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)

        {

            if (Block == null) return false;

            // Quern-style: tesselate static mesh explicitly, skip default aggregation.
            CompositeShape blockShape = Block.Shape;
            if (blockShape?.Base == null) return false;

            AssetLocation shapeLoc = blockShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = Shape.TryGet(Api, shapeLoc);
            if (shape == null) return false;

            tesselator.TesselateShape(
                Block,
                shape,
                out MeshData mesh,
                new Vec3f(blockShape.rotateX, blockShape.rotateY, blockShape.rotateZ),
                blockShape.QuantityElements,
                blockShape.SelectiveElements
            );
            mesher.AddMeshData(mesh);

            return true;

        }


        public void TryDiscoverNetwork()

        {

            if (Block?.Variant == null || !Block.Variant.TryGetValue("side", out string sideStr)) return;

            BlockFacing frontFace = BlockFacing.FromCode(sideStr);

            if (frontFace == null) return;

            // Keep network discovery aligned with SwitchboardBlock connector side.
            BlockFacing connectorFace = frontFace;



            var mechBase = GetBehavior<BEBehaviorMPBase>();

            mechBase?.CreateJoinAndDiscoverNetwork(connectorFace);

        }



        /// <summary>Same idea as <see cref="BlockEntityBellHammer.OnServerGameTick"/>: read TrueSpeed on the server and sync <see cref="PowerPercent"/> to clients with MarkDirty when the UI % or threshold state changes.</summary>
        private void OnServerSwitchboardTick(float dt)

        {

            TryApplyPersistedNetworkName();

            if (Api?.World?.BlockAccessor?.GetBlockEntity(Pos) != this)

            {

                return;

            }

            var consumer = GetBehavior<BEBehaviorMPConsumer>();

            float speed = consumer != null ? GameMath.Clamp(consumer.TrueSpeed, 0f, 1f) : 0f;

            PowerPercent = speed;

            WireNetworkKind currentKind = ResolveEffectiveNetworkKind();

            float minPower = WireNetworkTypeRules.GetRequirements(currentKind).MinPowerPercent;

            int percentDisplay = (int)(speed * 100f);

            bool aboveMin = speed >= minPower;

            bool intChanged = percentDisplay != _lastSyncedPowerPercent;

            bool thresholdChanged = aboveMin != _lastAboveMinPower;

            if (!intChanged && !thresholdChanged)

            {

                return;

            }

            _lastSyncedPowerPercent = percentDisplay;

            _lastAboveMinPower = aboveMin;

            MarkDirty();

            if (NetworkUID != 0)

            {

                WireNetworkHandler.RebuildNetworkState(NetworkUID);

            }

            if (IsDialogOpen())

            {

                dialog?.RefreshData();

            }

        }



        public override void ToTreeAttributes(ITreeAttribute tree)

        {

            if (Api?.Side == EnumAppSide.Server && NetworkUID != 0)

            {

                var n = WireNetworkHandler.GetNetwork(NetworkUID);

                if (n != null)

                {

                    persistedNetworkCustomName = n.CustomName ?? "";
                    WireNetworkHandler.SetPersistedNetworkName(NetworkUID, persistedNetworkCustomName);

                }

            }



            base.ToTreeAttributes(tree);

            tree.SetFloat("switchboardPowerPercent", PowerPercent);
            tree.SetBool("switchboardUsePowerRequirements", usePowerRequirements);

            tree.SetString("savedNetworkCustomName", persistedNetworkCustomName ?? "");

        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)

        {

            persistedNetworkCustomName = tree.GetString("savedNetworkCustomName", "");

            networkNameApplyPending = true;



            base.FromTreeAttributes(tree, worldForResolving);

            PowerPercent = tree.GetFloat("switchboardPowerPercent", 0f);
            usePowerRequirements = tree.GetBool("switchboardUsePowerRequirements", true);

            if (NetworkUID != 0)

            {

                WireNetworkHandler.SetPersistedNetworkName(NetworkUID, persistedNetworkCustomName);
                WireNetworkHandler.RebuildNetworkState(NetworkUID);

            }

            if (IsDialogOpen())

            {

                dialog?.RefreshData();

            }
            DisableConsumerInstancedRenderer();

        }



        /// <summary>

        /// Restores <see cref="CommunicationNetworkBase.CustomName"/> from chunk data once the in-memory network exists (server and client).

        /// </summary>

        private void TryApplyPersistedNetworkName()

        {

            if (!networkNameApplyPending)

            {

                return;

            }



            if (NetworkUID == 0)

            {

                return;

            }



            var net = WireNetworkHandler.GetNetwork(NetworkUID);

            if (net == null)

            {

                return;

            }



            WireNetworkHandler.SetPersistedNetworkName(NetworkUID, persistedNetworkCustomName);

            networkNameApplyPending = false;

        }



        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)

        {

            string displayName = GetNetworkDisplayName();

            if (!string.IsNullOrWhiteSpace(displayName))

            {

                dsc.AppendLine(UIUtils.I18n("Switchboard.NetworkName", displayName));

            }

        }



        public bool HasSufficientPowerFor(WireNetworkKind networkKind)

        {
            WireNetworkRequirements requirements = WireNetworkTypeRules.GetRequirements(networkKind);

            // Below mechanical threshold: toggle has no effect — network never counts as "powered" for advanced features.
            if (PowerPercent < requirements.MinPowerPercent)
            {
                return false;
            }

            // At or above threshold: advanced telegraph options are enabled only when the toggle is on.
            return usePowerRequirements;

        }



        public bool OnInteract()

        {

            if (Api?.Side != EnumAppSide.Client)

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



            dialog = new GuiDialogSwitchboard(capi, this);

            dialog.TryOpen();

            return true;

        }



        public string GetNetworkDisplayName()

        {

            if (NetworkUID == 0)

            {

                return "";

            }



            return WireNetworkHandler.GetDisplayName(NetworkUID);

        }



        /// <summary>Custom network label only (empty if unset). Never the numeric network id â€” for editable fields.</summary>

        public string GetNetworkCustomNameForEditor()

        {

            if (NetworkUID == 0)

            {

                return "";

            }



            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network != null)
            {
                return network.CustomName ?? "";
            }

            return WireNetworkHandler.GetPersistedNetworkName(NetworkUID);

        }



        public bool RenameNetwork(string name, out string failureLangKey)

        {

            if (NetworkUID == 0)

            {

                failureLangKey = "Network.NoNetwork";

                return false;

            }



            bool success = WireNetworkHandler.TryRenameNetwork(NetworkUID, name, out failureLangKey);

            if (success)

            {

                var net = WireNetworkHandler.GetNetwork(NetworkUID);

                persistedNetworkCustomName = net?.CustomName ?? "";
                WireNetworkHandler.SetPersistedNetworkName(NetworkUID, persistedNetworkCustomName);

                networkNameApplyPending = false;

                MarkDirty(true);

            }

            return success;

        }



        public void RequestRenameNetwork(string desiredName)

        {

            if (Api?.Side != EnumAppSide.Client)

            {

                return;

            }



            RPVoiceChatMod.SwitchboardClientChannel?.SendPacket(new SwitchboardRenameNetworkPacket

            {

                SwitchboardPos = Pos,

                NetworkName = desiredName ?? ""

            });

        }

        public void SetUsePowerRequirements(bool enabled)

        {

            if (usePowerRequirements == enabled)

            {

                return;

            }



            usePowerRequirements = enabled;

            MarkDirty(true);

            if (NetworkUID != 0)

            {

                WireNetworkHandler.RebuildNetworkState(NetworkUID);

            }

            if (IsDialogOpen())

            {

                dialog?.RefreshData();

            }

        }



        public void RequestSetUsePowerRequirements(bool enabled)

        {

            if (Api?.Side != EnumAppSide.Client)

            {

                return;

            }



            RPVoiceChatMod.SwitchboardClientChannel?.SendPacket(new SwitchboardPowerModePacket

            {

                SwitchboardPos = Pos,

                UsePowerRequirements = enabled

            });

        }



        public string[] GetConnectedLogicalNodeNames()

        {

            var network = WireNetworkHandler.GetNetwork(NetworkUID);

            if (network == null)

            {

                return Array.Empty<string>();

            }



            var entries = new List<string>();

            foreach (var node in network.Nodes.ToArray())

            {

                if (node == null || node is not IWireTypedNode typedNode)

                {

                    continue;

                }



                if (typedNode.WireNodeKind == WireNodeKind.Infrastructure || typedNode.WireNodeKind == WireNodeKind.Switchboard)

                {

                    continue;

                }



                string typeLabel = typedNode.WireNodeKind.ToString();

                string nodeLabel = node.Pos?.ToString() ?? "Unknown";

                if (node is ITelegraphEndpoint telegraphEndpoint && !string.IsNullOrWhiteSpace(telegraphEndpoint.CustomEndpointName))

                {

                    nodeLabel = telegraphEndpoint.CustomEndpointName;

                }



                entries.Add($"{typeLabel}: {nodeLabel}");

            }



            entries.Sort(StringComparer.OrdinalIgnoreCase);

            return entries.ToArray();

        }



        private bool IsDialogOpen()

        {

            return dialog != null && dialog.IsOpened();

        }



        private WireNetworkKind ResolveEffectiveNetworkKind()

        {

            var network = WireNetworkHandler.GetNetwork(NetworkUID);

            if (network == null || network.CurrentType == WireNetworkKind.None)

            {

                return WireNetworkKind.Telegraph;

            }



            return network.CurrentType;

        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            _mechPartRenderer?.Dispose();
            _mechPartRenderer = null;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            _mechPartRenderer?.Dispose();
            _mechPartRenderer = null;
        }

        private void DisableConsumerInstancedRenderer()
        {
            var consumer = GetBehavior<BEBehaviorMPConsumer>();
            if (consumer == null) return;
            consumer.Shape = null;
        }

        private float GetMechPartBaseRotY()
        {
            if (Block?.Variant?.TryGetValue("side", out string side) != true) return 0f;
            return side switch
            {
                "north" => 0f,
                "east" => 270f,
                "west" => 90f,
                "south" => 180f,
                _ => 0f
            };
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

            // Match in-game rotation handedness used by rotateY / rotateYByType.
            float x = dx * cos + dz * sin;
            float z = -dx * sin + dz * cos;

            return new Vec3f(x + 0.5f, point.Y, z + 0.5f);
        }

        private Vec3f RotateLocalOffsetByBlockSide(Vec3f offsetNorth)
        {
            return RotateAroundCenter(offsetNorth, GetBlockSideRotY());
        }

    }

}


