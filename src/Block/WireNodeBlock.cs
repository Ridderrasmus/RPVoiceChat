using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

using RPVoiceChat.GameContent.BlockEntity;

/// <summary>
/// Base class for blocks that use BEWireNode BlockEntity.
/// Provides common functionality like preventing breakage when connected.
/// </summary>
public abstract class WireNodeBlock : Block
{
    private static BlockPos lastWarningPos;
    private static long lastWarningTime;

    public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
    {
        // Check if the block has connections
        var wireNode = player.Entity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BEWireNode;
        if (wireNode != null && wireNode.GetConnections().Count > 0)
        {
            // Show warning message on client side (limit frequency)
            if (player.Entity.World.Api is ICoreClientAPI capi)
            {
                long currentTime = capi.World.ElapsedMilliseconds;

                // Only show warning if attacking the same block and not too frequently
                if (blockSel.Position != lastWarningPos)
                {
                    lastWarningPos = blockSel.Position;
                    lastWarningTime = 0;
                }

                if (currentTime - lastWarningTime >= 2000) // Show warning every 2 seconds max
                {
                    capi.TriggerIngameError(this, "wire-connected", UIUtils.I18n("Wire.CannotBreakConnected"));
                    lastWarningTime = currentTime;
                }
            }

            // Block has at least one connection, prevent breaking by returning current resistance
            // This prevents breaking progress because the resistance doesn't decrease
            return remainingResistance;
        }

        // No connections, allow normal breaking
        return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
    }
}

