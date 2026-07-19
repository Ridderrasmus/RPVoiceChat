using RPVoiceChat.GameContent.Block;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioAntennaPart : Vintagestory.API.Common.BlockEntity
    {
        private static readonly AssetLocation TopShapeLoc = new AssetLocation("rpvoicechat", "shapes/block/radioantenna/radioantenna_top.json");
        private static readonly AssetLocation PartShapeLoc = new AssetLocation("rpvoicechat", "shapes/block/radioantenna/radioantenna_part.json");

        private BlockPos baseEmitterPos;

        public BlockPos BaseEmitterPos => baseEmitterPos;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            ResolveBaseEmitter();
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            ResolveBaseEmitter();
            NotifyBaseEmitterRangeChanged();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            NotifyBaseEmitterRangeChanged();
        }

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
