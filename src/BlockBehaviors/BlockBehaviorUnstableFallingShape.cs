using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using static HarmonyLib.Code;

namespace RPVoiceChat.GameContent.BlockBehaviors
{
    // This class is "temporary" : the idea is to give the code that allow shape changing after falling to Vanilla Vintage Story
    // into genuine BlockBehaviorUnstableFalling 
    class BlockBehaviorUnstableFallingShape : BlockBehavior
    {
        private bool ignorePlaceTest;

        private AssetLocation[] exceptions;

        public bool fallSideways;

        private float dustIntensity;

        private float fallSidewaysChance = 0.3f;

        private AssetLocation fallSound;

        private float impactDamageMul;

        private Cuboidi[] attachmentAreas;

        private BlockFacing[] attachableFaces;

        public AssetLocation variantAfterFalling = null;

        public BlockBehaviorUnstableFallingShape(Block block)
            : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            attachableFaces = null;
            if (properties["attachableFaces"].Exists)
            {
                string[] array = properties["attachableFaces"].AsArray<string>();
                attachableFaces = new BlockFacing[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    attachableFaces[i] = BlockFacing.FromCode(array[i]);
                }
            }

            Dictionary<string, RotatableCube> dictionary = properties["attachmentAreas"].AsObject<Dictionary<string, RotatableCube>>();
            attachmentAreas = new Cuboidi[6];
            if (dictionary != null)
            {
                foreach (KeyValuePair<string, RotatableCube> item in dictionary)
                {
                    item.Value.Origin.Set(8.0, 8.0, 8.0);
                    BlockFacing blockFacing = BlockFacing.FromFirstLetter(item.Key[0]);
                    attachmentAreas[blockFacing.Index] = item.Value.RotatedCopy().ConvertToCuboidi();
                }
            }
            else
            {
                attachmentAreas[4] = properties["attachmentArea"].AsObject<Cuboidi>();
            }

            ignorePlaceTest = properties["ignorePlaceTest"].AsBool();
            exceptions = properties["exceptions"].AsObject(new AssetLocation[0], block.Code.Domain);
            fallSideways = properties["fallSideways"].AsBool();
            dustIntensity = properties["dustIntensity"].AsFloat();
            fallSidewaysChance = properties["fallSidewaysChance"].AsFloat(0.3f);
            impactDamageMul = properties["impactDamageMul"].AsFloat(1f);

            string text = properties["fallSound"].AsString();
            if (text != null)
            {
                fallSound = AssetLocation.Create(text, block.Code.Domain);
            }
            string textVariant = properties["variantAfterFalling"].AsString();
            if (textVariant != null)
            {
                variantAfterFalling = AssetLocation.Create(textVariant, block.Code.Domain);
            }
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PassThrough;
            if (ignorePlaceTest)
            {
                return true;
            }

            Cuboidi attachmentArea = attachmentAreas[4];
            BlockPos pos = blockSel.Position.DownCopy();
            Block block = world.BlockAccessor.GetBlock(pos);
            if (blockSel != null && !IsAttached(world.BlockAccessor, blockSel.Position) && !block.CanAttachBlockAt(world.BlockAccessor, base.block, pos, BlockFacing.UP, attachmentArea))
            {
                JsonObject attributes = base.block.Attributes;
                if ((attributes == null || !attributes["allowUnstablePlacement"].AsBool()) && !exceptions.Contains(block.Code))
                {
                    handling = EnumHandling.PreventSubsequent;
                    failureCode = "requiresolidground";
                    return false;
                }
            }

            return TryFalling(world, blockSel.Position, ref handling, ref failureCode);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handling)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos, ref handling);
            if (world.Side != EnumAppSide.Client)
            {
                EnumHandling handling2 = EnumHandling.PassThrough;
                string failureCode = "";
                TryFalling(world, pos, ref handling2, ref failureCode);
            }
        }

        private bool TryFalling(IWorldAccessor world, BlockPos pos, ref EnumHandling handling, ref string failureCode)
        {
            if (world.Side != EnumAppSide.Server)
            {
                return false;
            }

            if (!fallSideways && IsAttached(world.BlockAccessor, pos))
            {
                return false;
            }

            if (!((world as IServerWorldAccessor).Api as ICoreServerAPI).Server.Config.AllowFallingBlocks)
            {
                return false;
            }

            if (IsReplacableBeneath(world, pos) || (fallSideways && world.Rand.NextDouble() < (double)fallSidewaysChance && IsReplacableBeneathAndSideways(world, pos)))
            {
                if (world.GetNearestEntity(pos.ToVec3d().Add(0.5, 0.5, 0.5), 1f, 1.5f, (Entity e) => e is EntityBlockFalling entityBlockFalling && entityBlockFalling.initialPos.Equals(pos)) == null)
                {
                    if (variantAfterFalling != null)
                    {
                        Block fallenBlock = world.BlockAccessor.GetBlock(variantAfterFalling);
                        //world.BlockAccessor.ExchangeBlock(fallenBlock.BlockId, pos);
                        block.BlockId = fallenBlock.BlockId;
                    }
                    EntityBlockFalling entity = new EntityBlockFalling(block, world.BlockAccessor.GetBlockEntity(pos), pos, fallSound, impactDamageMul, canFallSideways: true, dustIntensity);
                    world.SpawnEntity(entity);
                    handling = EnumHandling.PreventSubsequent;
                    return true;
                }

                handling = EnumHandling.PreventDefault;
                failureCode = "entityintersecting";
                return false;
            }

            handling = EnumHandling.PassThrough;
            return false;
        }

        public virtual bool IsAttached(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockPos pos2;
            if (attachableFaces == null)
            {
                pos2 = pos.DownCopy();
                return blockAccessor.GetBlock(pos2).CanAttachBlockAt(blockAccessor, block, pos2, BlockFacing.UP, attachmentAreas[5]);
            }

            pos2 = new BlockPos();
            for (int i = 0; i < attachableFaces.Length; i++)
            {
                BlockFacing blockFacing = attachableFaces[i];
                pos2.Set(pos).Add(blockFacing);
                if (blockAccessor.GetBlock(pos2).CanAttachBlockAt(blockAccessor, block, pos2, blockFacing.Opposite, attachmentAreas[blockFacing.Index]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsReplacableBeneathAndSideways(IWorldAccessor world, BlockPos pos)
        {
            for (int i = 0; i < 4; i++)
            {
                BlockFacing blockFacing = BlockFacing.HORIZONTALS[i];
                Block blockOrNull = world.BlockAccessor.GetBlockOrNull(pos.X + blockFacing.Normali.X, pos.Y + blockFacing.Normali.Y, pos.Z + blockFacing.Normali.Z);
                if (blockOrNull != null && blockOrNull.Replaceable >= 6000)
                {
                    blockOrNull = world.BlockAccessor.GetBlockOrNull(pos.X + blockFacing.Normali.X, pos.Y + blockFacing.Normali.Y - 1, pos.Z + blockFacing.Normali.Z);
                    if (blockOrNull != null && blockOrNull.Replaceable >= 6000)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsReplacableBeneath(IWorldAccessor world, BlockPos pos)
        {
            return world.BlockAccessor.GetBlockBelow(pos).Replaceable > 6000;
        }
    }
}
