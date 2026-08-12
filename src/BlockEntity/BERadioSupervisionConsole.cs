using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Gui;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioSupervisionConsole : BEWireNode, INetworkRoot, IWireTypedNode
    {
        private RadioConsoleDialog dialog;
        private string frequency = "100.0";
        private string displayName = "";
        private long originalCreatedNetworkID;

        public override bool IsActiveEndpoint => true;
        protected override int MaxConnections => 2;
        public WireNodeKind WireNodeKind => WireNodeKind.RadioConsole;
        public long CreatedNetworkID => originalCreatedNetworkID;

        public string Frequency => frequency ?? "";
        public string DisplayName => displayName ?? "";

        /// <summary>
        /// Wire attachment centers for Axis1 / Axis3 (left / right when facing north),
        /// from the radioconsole shape including the 45° local Y rotation.
        /// </summary>
        private static readonly Vec3f[] AxisOffsetsNorth =
        {
            ComputeRotatedAxisCenter(5.0f, 6.0f, 14.0f, 15.25f, -2.0f, -1.0f, 10.0f, 1.0f, 45f),
            ComputeRotatedAxisCenter(11.0f, 12.0f, 14.0f, 15.25f, -2.0f, -1.0f, 16.0f, 1.0f, 45f)
        };

        public override Vec3f GetWireAttachmentOffsetFor(BlockPos otherNodePos)
        {
            if (otherNodePos == null)
            {
                return base.GetWireAttachmentOffsetFor(otherNodePos);
            }

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

            if (entries.Count == 0)
            {
                return base.GetWireAttachmentOffsetFor(otherNodePos);
            }

            var assignedAxisByNode = AssignAxesByBestGlobalDistance(entries, axisOffsetsCurrent);
            if (!assignedAxisByNode.TryGetValue((otherNodePos.X, otherNodePos.Y, otherNodePos.Z), out int index))
            {
                return base.GetWireAttachmentOffsetFor(otherNodePos);
            }

            index = GameMath.Clamp(index, 0, AxisOffsetsNorth.Length - 1);
            return axisOffsetsCurrent[index];
        }

        private Dictionary<(int X, int Y, int Z), int> AssignAxesByBestGlobalDistance(
            IReadOnlyList<(BlockPos Pos, Vec3f LocalCurrent)> entries,
            IReadOnlyList<Vec3f> axisOffsetsCurrent)
        {
            int count = Math.Min(entries.Count, axisOffsetsCurrent.Count);
            var assignedAxisByNode = new Dictionary<(int X, int Y, int Z), int>();
            if (count == 0)
            {
                return assignedAxisByNode;
            }

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

                for (int axis = 0; axis < axisOffsetsCurrent.Count; axis++)
                {
                    if (usedAxes[axis])
                    {
                        continue;
                    }

                    float nextCost = cost + DistanceSqXZ(entries[depth].LocalCurrent, axisOffsetsCurrent[axis]);
                    if (nextCost >= bestCost)
                    {
                        continue;
                    }

                    usedAxes[axis] = true;
                    current[depth] = axis;
                    Search(depth + 1, nextCost);
                    usedAxes[axis] = false;
                }
            }

            Search(0, 0f);

            for (int i = 0; i < count; i++)
            {
                int axisIndex = best[i];
                if (axisIndex < 0)
                {
                    continue;
                }

                assignedAxisByNode[(entries[i].Pos.X, entries[i].Pos.Y, entries[i].Pos.Z)] = axisIndex;
            }

            return assignedAxisByNode;
        }

        private static float DistanceSqXZ(Vec3f a, Vec3f b)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private static Vec3f ComputeRotatedAxisCenter(
            float fromX, float toX,
            float fromY, float toY,
            float fromZ, float toZ,
            float rotationOriginX, float rotationOriginZ,
            float rotYDeg)
        {
            float cx = (fromX + toX) * 0.5f / 16f;
            float cy = (fromY + toY) * 0.5f / 16f;
            float cz = (fromZ + toZ) * 0.5f / 16f;
            var center = new Vec3f(cx, cy, cz);
            var origin = new Vec3f(rotationOriginX / 16f, cy, rotationOriginZ / 16f);
            return RotateAroundPointXZ(center, origin, rotYDeg);
        }

        private static Vec3f RotateAroundPointXZ(Vec3f point, Vec3f origin, float rotDeg)
        {
            if (Math.Abs(rotDeg) < 0.001f)
            {
                return point;
            }

            float rad = rotDeg * GameMath.DEG2RAD;
            float cos = GameMath.Cos(rad);
            float sin = GameMath.Sin(rad);

            float dx = point.X - origin.X;
            float dz = point.Z - origin.Z;
            float x = dx * cos + dz * sin;
            float z = -dx * sin + dz * cos;

            return new Vec3f(x + origin.X, point.Y, z + origin.Z);
        }

        private static Vec3f RotateAroundCenter(Vec3f point, float rotDeg)
        {
            if (Math.Abs(rotDeg) < 0.001f)
            {
                return point;
            }

            float rad = rotDeg * GameMath.DEG2RAD;
            float cos = GameMath.Cos(rad);
            float sin = GameMath.Sin(rad);

            float dx = point.X - 0.5f;
            float dz = point.Z - 0.5f;
            float x = dx * cos + dz * sin;
            float z = -dx * sin + dz * cos;

            return new Vec3f(x + 0.5f, point.Y, z + 0.5f);
        }

        private Vec3f RotateLocalOffsetByBlockSide(Vec3f offsetNorth)
        {
            float rotY = Block?.Variant?.TryGetValue("side", out string side) == true
                ? side switch
                {
                    "north" => 0f,
                    "east" => 270f,
                    "west" => 90f,
                    "south" => 180f,
                    _ => 0f
                }
                : 0f;

            return RotateAroundCenter(offsetNorth, rotY);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.RegisterSupervisionConsole(Pos);
            }
        }

        public override void OnNetworkCreated(long networkID)
        {
            base.OnNetworkCreated(networkID);
            if (originalCreatedNetworkID == 0)
            {
                originalCreatedNetworkID = networkID;
                MarkDirty();
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

            dialog = new RadioConsoleDialog(capi, this);
            dialog.TryOpen();
            return true;
        }

        public void RequestSetFrequency(string desired)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetFrequency,
                Value = desired ?? ""
            });
        }

        public void RequestSetDisplayName(string desired)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetDisplayName,
                Value = desired ?? ""
            });
        }

        public void RequestSaveSettings(string desiredFrequency, string desiredDisplayName)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RequestSetFrequency(desiredFrequency);
            RequestSetDisplayName(desiredDisplayName);
        }

        /// <returns>False when the frequency is already claimed by another transmitter.</returns>
        public bool TrySetFrequency(string desired)
        {
            string normalized = (desired ?? "").Trim();
            if (RadioFrequencyUtil.Matches(normalized, frequency))
            {
                return true;
            }

            if (Api?.World != null && !RadioTransmitFrequencyGuard.IsFrequencyAvailable(Api.World, normalized, Pos))
            {
                return false;
            }

            frequency = normalized;
            MarkDirty();
            dialog?.RefreshData();
            return true;
        }

        public void SetDisplayName(string desired)
        {
            displayName = (desired ?? "").Trim();
            MarkDirty();
            dialog?.RefreshData();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            frequency = tree.GetString("rpvc:radioFrequency", frequency);
            displayName = tree.GetString("rpvc:radioDisplayName", displayName);
            originalCreatedNetworkID = tree.GetLong("rpvc:radioConsoleCreatedNetworkId", originalCreatedNetworkID);
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("rpvc:radioFrequency", frequency ?? "");
            tree.SetString("rpvc:radioDisplayName", displayName ?? "");
            if (originalCreatedNetworkID != 0)
            {
                tree.SetLong("rpvc:radioConsoleCreatedNetworkId", originalCreatedNetworkID);
            }
        }

        public override void OnBlockRemoved()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterSupervisionConsole(Pos);
            }

            base.OnBlockRemoved();
            dialog?.TryClose();
        }

        public override void OnBlockUnloaded()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterSupervisionConsole(Pos);
            }

            base.OnBlockUnloaded();
        }
    }
}
