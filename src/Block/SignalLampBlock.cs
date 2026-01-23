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
            if (world.Side != EnumAppSide.Server) return true;

            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            var lightBehavior = blockEntity?.GetBehavior<BEBehaviorLightSource>();
            if (lightBehavior != null)
            {
                // Activate the light
                lightBehavior.SetLightActive(true);
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
            if (world.Side != EnumAppSide.Server) return true;

            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            var lightBehavior = blockEntity?.GetBehavior<BEBehaviorLightSource>();
            if (lightBehavior != null)
            {
                // Deactivate the light if the interaction is cancelled
                lightBehavior.SetLightActive(false);
            }

            return true;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.Side != EnumAppSide.Server) return;

            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            var lightBehavior = blockEntity?.GetBehavior<BEBehaviorLightSource>();
            if (lightBehavior != null)
            {
                // Deactivate the light when the click is released
                lightBehavior.SetLightActive(false);
            }
        }
    }
}
