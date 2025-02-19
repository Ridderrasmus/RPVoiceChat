
using Newtonsoft.Json.Linq;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RPVoiceChat.GameContent.BlockEntityBehaviors
{
    public class BlockEntityBehaviorRegistry
    {
        public static void RegisterBlockEntityBehaviors(ICoreAPI api)
        {
            api.RegisterBlockEntityBehaviorClass("BERingable", typeof(BEBehaviorRingable));
        }

        public static void AddBlockEntityBehaviors(ICoreAPI api)
        {
            // If block has "Door" behaviour on it add the Ringable behaviour
            foreach (Block block in api.World.Blocks.Where(x => x != null && x.Code != null && x.BlockBehaviors.Any(behaviour => behaviour.GetType().Name == "BlockBehaviorDoor" || behaviour.GetType().Name == "BlockBehaviorTrapDoor")))
            {
                
                // Add the BEBehaviourRingable behaviour to the block
                BlockEntityBehaviorType behaviour = new BlockEntityBehaviorType()
                {
                    Name = "BERingable"
                };
                behaviour.properties = new JsonObject(new JObject());

                behaviour.properties.Token["bellPartCode"] = (block.Attributes["bellPartCode"].Exists) ? block.Attributes["bellPartCode"].AsString() : JToken.FromObject("");

                block.BlockEntityBehaviors = block.BlockEntityBehaviors.Append(behaviour).Reverse().ToArray();

                if (block.EntityClass == null)
                {
                    block.EntityClass = "Generic";
                }

            }
        }
    }
}
