using System;
using RPVoiceChat.GameContent;
using RPVoiceChat.GameContent.Block;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioAntennaPart : Vintagestory.API.Common.BlockEntity, IBlockEntityWithCustomLightPosition
    {
        private static readonly AssetLocation TopShapeLoc = new AssetLocation("rpvoicechat", "shapes/block/radioantenna/radioantenna_top.json");
        private static readonly AssetLocation PartShapeLoc = new AssetLocation("rpvoicechat", "shapes/block/radioantenna/radioantenna_part.json");

        /// <summary>Center of the hollow bulb mass on radioantenna_top (shape units / 16).</summary>
        private static readonly Vec3d BulbLightLocalOffset = new Vec3d(0.5, 13.0 / 16.0, 0.5);

        /// <summary>Wind-turbine style obstruction light: ~2.5 s fade cycle.</summary>
        private const float BeaconPulsePeriodSeconds = 2.5f;

        private BlockPos baseEmitterPos;
        private long beaconTickId;

        public BlockPos BaseEmitterPos => baseEmitterPos;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            ResolveBaseEmitter();

            // Server drives on/off (synced); client applies the smooth pulse.
            int intervalMs = api.Side == EnumAppSide.Server ? 250 : 50;
            beaconTickId = RegisterGameTickListener(OnBeaconTick, intervalMs);
            if (api.Side == EnumAppSide.Server)
            {
                SyncBeaconPowered();
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            ResolveBaseEmitter();
            NotifyBaseEmitterRangeChanged();
            if (Api?.Side == EnumAppSide.Server)
            {
                SyncBeaconPowered();
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            NotifyBaseEmitterRangeChanged();
        }

        public override void OnBlockUnloaded()
        {
            if (beaconTickId != 0)
            {
                UnregisterGameTickListener(beaconTickId);
                beaconTickId = 0;
            }

            base.OnBlockUnloaded();
        }

        public Vec3d GetLightOrigin() => Pos.ToVec3d().Add(BulbLightLocalOffset.X, BulbLightLocalOffset.Y, BulbLightLocalOffset.Z);

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            int x = tree.GetInt("rpvc:radioAntennaBaseX", int.MinValue);
            int y = tree.GetInt("rpvc:radioAntennaBaseY", int.MinValue);
            int z = tree.GetInt("rpvc:radioAntennaBaseZ", int.MinValue);
            baseEmitterPos = x == int.MinValue ? null : new BlockPos(x, y, z);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (baseEmitterPos != null)
            {
                tree.SetInt("rpvc:radioAntennaBaseX", baseEmitterPos.X);
                tree.SetInt("rpvc:radioAntennaBaseY", baseEmitterPos.Y);
                tree.SetInt("rpvc:radioAntennaBaseZ", baseEmitterPos.Z);
            }
        }

        /// <summary>
        /// Tip of the stack renders radioantenna_top; any segment with another part above renders radioantenna_part.
        /// </summary>
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Block == null || Api == null)
            {
                return false;
            }

            bool hasAntennaAbove = Api.World.BlockAccessor.GetBlock(Pos.UpCopy()) is RadioAntennaPartBlock;
            AssetLocation shapeLoc = hasAntennaAbove ? PartShapeLoc : TopShapeLoc;
            Shape shape = Shape.TryGet(Api, shapeLoc);
            if (shape == null)
            {
                return false;
            }

            CompositeShape blockShape = Block.Shape;
            tesselator.TesselateShape(
                Block,
                shape,
                out MeshData mesh,
                new Vec3f(blockShape?.rotateX ?? 0, blockShape?.rotateY ?? 0, blockShape?.rotateZ ?? 0),
                blockShape?.QuantityElements,
                blockShape?.SelectiveElements);

            mesher.AddMeshData(mesh);
            // Keep behavior meshes (e.g. Coverable wall/ceiling cover) while skipping the default block shape.
            base.OnTesselation(mesher, tesselator);
            return true;
        }

        public void ResolveBaseEmitter()
        {
            if (Api?.World?.BlockAccessor == null)
            {
                return;
            }

            BlockPos scan = Pos.DownCopy();
            while (scan.Y >= 0)
            {
                var emitter = Api.World.BlockAccessor.GetBlockEntity(scan) as BlockEntityRadioEmitter;
                if (emitter != null)
                {
                    baseEmitterPos = scan.Copy();
                    MarkDirty();
                    return;
                }

                var part = Api.World.BlockAccessor.GetBlockEntity(scan) as BlockEntityRadioAntennaPart;
                if (part?.BaseEmitterPos != null)
                {
                    baseEmitterPos = part.BaseEmitterPos.Copy();
                    MarkDirty();
                    return;
                }

                if (part == null)
                {
                    break;
                }

                scan = scan.DownCopy();
            }

            baseEmitterPos = null;
            MarkDirty();
        }

        private void OnBeaconTick(float dt)
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                SyncBeaconPowered();
                return;
            }

            PulseBeaconLight();
        }

        private void SyncBeaconPowered()
        {
            var light = GetBehavior<BEBehaviorLightable>();
            if (light == null)
            {
                return;
            }

            bool isTip = Api.World.BlockAccessor.GetBlock(Pos.UpCopy()) is not RadioAntennaPartBlock;
            bool shouldLit = isTip && EmitterHasSufficientTransmitPower();
            light.SetLightActive(shouldLit);
        }

        private void PulseBeaconLight()
        {
            var light = GetBehavior<BEBehaviorLightable>();
            if (light == null)
            {
                return;
            }

            if (!light.IsLightActive)
            {
                light.SetClientPulseFactor(0f);
                return;
            }

            // Smooth aviation-style pulse: fade in/out (not a hard blink).
            double phase = (Api.World.ElapsedMilliseconds / 1000.0) % BeaconPulsePeriodSeconds / BeaconPulsePeriodSeconds;
            float pulse = (float)(0.5 * (1.0 + Math.Sin(phase * Math.PI * 2.0 - Math.PI / 2.0)));
            pulse = GameMath.Clamp((pulse - 0.08f) / 0.92f, 0f, 1f);
            light.SetClientPulseFactor(0.15f + pulse * 0.85f);
        }

        private bool EmitterHasSufficientTransmitPower()
        {
            if (baseEmitterPos == null && Api != null)
            {
                ResolveBaseEmitter();
            }

            if (baseEmitterPos == null || Api?.World?.BlockAccessor == null)
            {
                return false;
            }

            return Api.World.BlockAccessor.GetBlockEntity(baseEmitterPos) is BlockEntityRadioEmitter emitter
                && emitter.HasSufficientTransmitPower();
        }

        private void NotifyBaseEmitterRangeChanged()
        {
            if (baseEmitterPos == null || Api?.World?.BlockAccessor == null)
            {
                return;
            }

            if (Api.World.BlockAccessor.GetBlockEntity(baseEmitterPos) is BlockEntityRadioEmitter emitter)
            {
                emitter.OnAntennaStackChanged();
            }
        }
    }
}
