using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace RPVoiceChat.GameContent.BlockBehaviours
{
    public class BlockBehaviourRegistry
    {

        public static void RegisterBlockEntityBehaviours(ICoreAPI api)
        {
            api.RegisterBlockBehaviorClass("Ringable", typeof(BehaviourRingable));
        }

        public static void AddBehaviours(ICoreAPI api)
        {
            // If block has "Door" behaviour on it add the Ringable behaviour
            foreach (Block block in api.World.Blocks.Where(x => x != null && x.Code != null && x.BlockBehaviors.Any(behaviour => behaviour.GetType().Name == "BlockBehaviorDoor" || behaviour.GetType().Name == "BlockBehaviorTrapDoor")))
            {
                BehaviourRingable behaviour = new BehaviourRingable(block);
                block.CollectibleBehaviors = block.CollectibleBehaviors.Append(behaviour);
                block.BlockBehaviors = block.BlockBehaviors.Append(behaviour);
            }
        }
    }
}
