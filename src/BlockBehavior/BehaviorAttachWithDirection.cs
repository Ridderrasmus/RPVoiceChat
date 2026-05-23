using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

/// <summary>
/// Custom attach-and-orient behavior for two-axis block variants (attachment face + horizontal side).
/// This implementation is heavily inspired by Len Kagamine's class
/// <c>BlockBehaviorCoverWithDirection</c>:
/// https://raw.githubusercontent.com/V2LenKagamine/LensVintageModding/refs/heads/main/TemporalMachinations/TempMach/tempmach/src/behaviors/CoverWDir.cs
/// Many thanks to Len Kagamine for sharing this approach.
/// </summary>
class BehaviorAttachWithDirection : BlockBehavior
{
    private string orientationCode = "orientation";
    private string sideCode = "side";
    private string dropOrientation = "up";
    private string dropSide = "north";

    public BehaviorAttachWithDirection(Block block) : base(block)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        orientationCode = properties["facingCode"].AsString("orientation");
        sideCode = properties["sideCode"].AsString("side");
        dropOrientation = properties["dropOrientation"].AsString("up");
        dropSide = properties["dropSide"].AsString("north");
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
    {
        handling = EnumHandling.PreventDefault;
        Block orientedBlock = GetOrientedBlock(world, byPlayer, blockSel.Face);
        if (orientedBlock == null)
        {
            failureCode = "requireattachable";
            return false;
        }

        if (!orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
        {
            return false;
        }

        orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
        return true;
    }

    public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
    {
        handling = EnumHandling.PreventDefault;

        BlockFacing oppositeFace = blockSel.Face.Opposite;
        BlockPos attachingBlockPos = blockSel.Position.AddCopy(oppositeFace);
        Block supportBlock = world.BlockAccessor.GetBlock(attachingBlockPos);

        if (supportBlock.CanAttachBlockAt(world.BlockAccessor, block, attachingBlockPos, blockSel.Face))
        {
            return true;
        }

        failureCode = "requireattachable";
        return false;
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropQuantityMultiplier, ref EnumHandling handled)
    {
        handled = EnumHandling.PreventDefault;
        Block droppedBlock = world.BlockAccessor.GetBlock(block.CodeWithVariants(
            new[] { orientationCode, sideCode },
            new[] { dropOrientation, dropSide }
        ));
        return new[] { new ItemStack(droppedBlock) };
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
    {
        handled = EnumHandling.PreventDefault;
        Block pickedBlock = world.BlockAccessor.GetBlock(block.CodeWithVariants(
            new[] { orientationCode, sideCode },
            new[] { dropOrientation, dropSide }
        ));
        return new ItemStack(pickedBlock);
    }

    private Block GetOrientedBlock(IWorldAccessor world, IPlayer byPlayer, BlockFacing attachFace)
    {
        string orientation = attachFace.Code;
        string side = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw).Code;
        return world.BlockAccessor.GetBlock(block.CodeWithVariants(
            new[] { orientationCode, sideCode },
            new[] { orientation, side }
        ));
    }
}
