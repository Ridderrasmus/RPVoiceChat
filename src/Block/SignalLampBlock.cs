using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Block
{
    /// <summary>
    /// Signal Lamp block that only lights up when right-click is held
    /// </summary>
    public class SignalLampBlock : Vintagestory.API.Common.Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySignalLamp;
            if (blockEntity != null)
            {
                if (world.Side == EnumAppSide.Server)
                {
                    var lightBehavior = blockEntity.GetBehavior<BEBehaviorLightable>();
                    if (lightBehavior != null)
                    {
                        // Activate the light on server side
                        lightBehavior.SetLightActive(true);
                    }
                    // Start animation on server side for synchronization
                    blockEntity.StartSlatesAnimation();
                }
                else
                {
                    // Start animation on client side for immediate visual feedback
                    blockEntity.StartSlatesAnimation();
                }
            }

            return true; // Return true to continue the interaction
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Keep the light active as long as the click is held
            return true;
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySignalLamp;
            if (blockEntity != null)
            {
                if (world.Side == EnumAppSide.Server)
                {
                    var lightBehavior = blockEntity.GetBehavior<BEBehaviorLightable>();
                    if (lightBehavior != null)
                    {
                        // Deactivate the light if the interaction is cancelled
                        lightBehavior.SetLightActive(false);
                    }
                    // Stop animation on server side
                    blockEntity.StopSlatesAnimation();
                }
                else
                {
                    // Stop the animation on client side
                    blockEntity.StopSlatesAnimation();
                }
            }

            return true;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySignalLamp;
            if (blockEntity != null)
            {
                if (world.Side == EnumAppSide.Server)
                {
                    var lightBehavior = blockEntity.GetBehavior<BEBehaviorLightable>();
                    if (lightBehavior != null)
                    {
                        // Deactivate the light when the click is released
                        lightBehavior.SetLightActive(false);
                    }
                    // Stop animation on server side
                    blockEntity.StopSlatesAnimation();
                }
                else
                {
                    // Stop the animation on client side
                    blockEntity.StopSlatesAnimation();
                }
            }
        }
    }
}
