using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace RPVoiceChat.GameContent.BlockBehaviors
{
    public class BlockBehaviorRegistry
    {

        public static void RegisterBlockEntityBehaviors(ICoreAPI api)
        {
            api.RegisterBlockBehaviorClass("Ringable", typeof(BehaviorRingable));
        }

        public static void AddBehaviors(ICoreAPI api)
        {
            // If block has "Door" behaviour on it add the Ringable behaviour
            foreach (Block block in api.World.Blocks.Where(x => x != null && x.Code != null && x.BlockBehaviors.Any(behaviour => behaviour.GetType().Name == "BlockBehaviorDoor" || behaviour.GetType().Name == "BlockBehaviorTrapDoor")))
            {
                BehaviorRingable behaviour = new BehaviorRingable(block);
                block.CollectibleBehaviors = block.CollectibleBehaviors.Append(behaviour);
                block.BlockBehaviors = block.BlockBehaviors.Append(behaviour);
            }
        }
    }
}
