using RPVoiceChat.Systems;
using System;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Blocks
{
    public class WireNode : Block
    {
        public int NetworkUID { get; set; } = 0;
        public long NodeUID { get; set; }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            NodeUID = blockPos.AsLong;
            WireNetworkHandler.AddNewNetwork(this);
        }

        public virtual void OnRecievedSignal(string message)
        {

        }

        
    }
}
