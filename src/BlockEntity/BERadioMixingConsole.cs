using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public enum MixingConsoleOnAirResult
    {
        Success,
        AlreadyOnAir,
        NotOperator,
        NotWired,
        NoBroadcastPath
    }

    public class BlockEntityRadioMixingConsole : BEWireNode, IWireTypedNode, IRadioProgramSource, IBlockEntityWithCustomLightPosition
    {
        private static readonly AssetLocation OnAirGlassOffTexture = new("rpvoicechat:block/radio/onairglass");
        private static readonly AssetLocation OnAirGlassOnTexture = new("rpvoicechat:block/radio/onairglass-on");

        private const int MaxHlsUrlLength = 2048;

        private RadioMixingConsoleDialog dialog;
        private string hlsStreamUrl = "";
        private bool isOnAir;
        private string activeOperatorPlayerUid = "";

        public override bool IsActiveEndpoint => true;
        protected override int MaxConnections => 3;
        public WireNodeKind WireNodeKind => WireNodeKind.Radio;

        /// <summary>
        /// Wire attachment centers for Axis1 / Axis3 / Axis2 (left / middle / right when facing north),
        /// computed from the radiomixingconsole shape including the 45° local Y rotation.
        /// </summary>
        private static readonly Vec3f[] AxisOffsetsNorth =
        {
            ComputeRotatedAxisCenter(4.0f, 5.0f, -2.0f, -1.0f, 9.0f, 1.0f, 45f),
            ComputeRotatedAxisCenter(8.0f, 9.0f, -2.0f, -1.0f, 13.0f, 1.0f, 45f),
            ComputeRotatedAxisCenter(12.0f, 13.0f, -2.0f, -1.0f, 17.0f, 1.0f, 45f)
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
            float fromZ, float toZ,
            float rotationOriginX, float rotationOriginZ,
            float rotYDeg)
        {
            float cx = (fromX + toX) * 0.5f / 16f;
            float cy = (5.5f + 6.75f) * 0.5f / 16f;
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

        public bool IsOnAir => isOnAir;
        public string HlsStreamUrl => hlsStreamUrl ?? "";
        public string ActiveOperatorPlayerUid => activeOperatorPlayerUid ?? "";

        public string ProgramRouteKey => RadioProgramRouteKey.ForMixingConsole(Pos);

        public bool IsBusyForOtherPlayer(string playerUid)
        {
            return isOnAir && !IsOperator(playerUid);
        }

        public bool HasWiredBroadcastPath()
        {
            if (NetworkUID == 0)
            {
                return false;
            }

            return RadioWireNetworkHelper.FindEmitters(this).Any()
                || RadioWireNetworkHelper.FindSpeakers(this).Any();
        }

        public bool HasActiveBroadcastOutput()
        {
            bool hasSpeaker = RadioWireNetworkHelper.FindSpeakers(this).Any();
            bool hasPoweredEmitter = RadioWireNetworkHelper.FindEmitters(this).Any(emitter => emitter.IsWirelessTransmitting);
            return hasSpeaker || hasPoweredEmitter;
        }

        public bool IsOperator(string playerUid)
        {
            if (!isOnAir || string.IsNullOrWhiteSpace(activeOperatorPlayerUid))
            {
                return true;
            }

            if (Api?.Side == EnumAppSide.Server && Api.World.PlayerByUid(activeOperatorPlayerUid) == null)
            {
                return true;
            }

            return activeOperatorPlayerUid == playerUid;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            OnConnectionsChanged += OnRadioWireConnectionsChanged;
            if (api.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.RegisterMixingConsole(Pos);
            }

            SyncOnAirVisuals();
        }

        public Vec3d GetLightOrigin()
        {
            // Approximate OnAir lamp on the console board (shape units → block space).
            Vec3f local = RotateLocalOffsetByBlockSide(new Vec3f(0.5f, 0.72f, 0.42f));
            return Pos.ToVec3d().Add(local.X, local.Y, local.Z);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Api is not ICoreClientAPI || Block?.Shape?.Base == null)
            {
                return false;
            }

            CompositeShape blockShape = Block.Shape;
            AssetLocation shapeLoc = blockShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = Shape.TryGet(Api, shapeLoc);
            if (shape == null)
            {
                return false;
            }

            var textures = new Dictionary<string, AssetLocation>();
            if (Block.Textures != null)
            {
                foreach (var entry in Block.Textures)
                {
                    AssetLocation loc = entry.Value?.Base ?? entry.Value?.Baked?.BakedName;
                    if (loc != null)
                    {
                        textures[entry.Key] = loc;
                    }
                }
            }

            textures["onairglass"] = isOnAir ? OnAirGlassOnTexture : OnAirGlassOffTexture;

            var texSource = new ContainedTextureSource(
                (ICoreClientAPI)Api,
                ((ICoreClientAPI)Api).BlockTextureAtlas,
                textures,
                $"For block {Block.Code}");

            tesselator.TesselateShape(
                "rpvoicechat:radiomixingconsole",
                shape,
                out MeshData mesh,
                texSource,
                new Vec3f(blockShape.rotateX, blockShape.rotateY, blockShape.rotateZ));
            mesher.AddMeshData(mesh);
            return true;
        }

        private void SyncOnAirVisuals()
        {
            var light = GetBehavior<BEBehaviorLightable>();
            light?.SetLightActive(isOnAir);
            if (isOnAir)
            {
                light?.SetLightColor(new Vec3f(1f, 0.18f, 0.1f));
                light?.SetLightLevel(0.45f);
            }
        }

        private void OnRadioWireConnectionsChanged()
        {
            if (Api is ICoreServerAPI sapi)
            {
                WireTopologyConnectivity.NotifyNode(sapi, this);
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

            dialog = new RadioMixingConsoleDialog(capi, this);
            dialog.TryOpen();
            return true;
        }

        public void RequestSetHlsUrl(string desired)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetMixingConsoleHlsUrl,
                Value = desired ?? ""
            });
        }

        public void RequestSetOnAir(bool enabled)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetMixingConsoleOnAir,
                IntValue = enabled ? 1 : 0
            });
        }

        public bool SetHlsUrl(string desired)
        {
            string normalized = NormalizeHlsUrl(desired);
            if (normalized == null)
            {
                return false;
            }

            hlsStreamUrl = normalized;
            MarkDirty(true);
            dialog?.RefreshData();
            return true;
        }

        public MixingConsoleOnAirResult SetOnAir(IPlayer byPlayer, bool enabled)
        {
            if (Api?.Side != EnumAppSide.Server || byPlayer == null)
            {
                return MixingConsoleOnAirResult.NotOperator;
            }

            if (enabled)
            {
                if (isOnAir)
                {
                    return MixingConsoleOnAirResult.AlreadyOnAir;
                }

                if (NetworkUID == 0)
                {
                    return MixingConsoleOnAirResult.NotWired;
                }

                if (!HasWiredBroadcastPath())
                {
                    return MixingConsoleOnAirResult.NoBroadcastPath;
                }

                isOnAir = true;
                activeOperatorPlayerUid = byPlayer.PlayerUID;
            }
            else
            {
                if (!IsOperator(byPlayer.PlayerUID))
                {
                    return MixingConsoleOnAirResult.NotOperator;
                }

                ClearOnAirInternal();
            }

            MarkDirty(true);
            SyncOnAirVisuals();
            return MixingConsoleOnAirResult.Success;
        }

        public static string GetOnAirFailureLangKey(MixingConsoleOnAirResult result)
        {
            return result switch
            {
                MixingConsoleOnAirResult.AlreadyOnAir => "Radio.MixingConsole.Error.Busy",
                MixingConsoleOnAirResult.NotOperator => "Radio.MixingConsole.Error.NotOperator",
                MixingConsoleOnAirResult.NotWired => "Radio.MixingConsole.Error.NotWired",
                MixingConsoleOnAirResult.NoBroadcastPath => "Radio.MixingConsole.Error.NoBroadcastPath",
                _ => null
            };
        }

        public void ClearOnAir()
        {
            if (!isOnAir && string.IsNullOrWhiteSpace(activeOperatorPlayerUid))
            {
                return;
            }

            ClearOnAirInternal();
            MarkDirty(true);
            SyncOnAirVisuals();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<RadioVoiceRoutingSystem>()?.ClearProgramRoute(ProgramRouteKey);
            }
        }

        private void ClearOnAirInternal()
        {
            isOnAir = false;
            activeOperatorPlayerUid = "";
        }

        public static string NormalizeHlsUrl(string desired)
        {
            if (desired == null)
            {
                return "";
            }

            string trimmed = desired.Trim();
            if (trimmed.Length == 0)
            {
                return "";
            }

            if (trimmed.Length > MaxHlsUrlLength)
            {
                return null;
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            return uri.AbsoluteUri;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            hlsStreamUrl = tree.GetString("rpvc:mixingConsoleHlsUrl", hlsStreamUrl);
            isOnAir = tree.GetBool("rpvc:mixingConsoleOnAir", false);
            activeOperatorPlayerUid = tree.GetString("rpvc:mixingConsoleOperatorUid", "");
            SyncOnAirVisuals();
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("rpvc:mixingConsoleHlsUrl", hlsStreamUrl ?? "");
            tree.SetBool("rpvc:mixingConsoleOnAir", isOnAir);
            tree.SetString("rpvc:mixingConsoleOperatorUid", activeOperatorPlayerUid ?? "");
        }

        public override void OnBlockRemoved()
        {
            ClearOnAir();
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterMixingConsole(Pos);
            }

            base.OnBlockRemoved();
            dialog?.TryClose();
        }

        public override void OnBlockUnloaded()
        {
            // Do NOT ClearOnAir here — that was wiping the persisted On Air flag on chunk unload.
            // Runtime sessions stop when the console leaves RadioBlockIndex; On Air is restored on reload.
            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<RadioVoiceRoutingSystem>()?.ClearProgramRoute(ProgramRouteKey);
                RadioBlockIndex.UnregisterMixingConsole(Pos);
            }

            base.OnBlockUnloaded();
        }
    }
}
