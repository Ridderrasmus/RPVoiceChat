
using Newtonsoft.Json.Linq;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

public class BlockEntityBehaviorRegistry
{
    public static void RegisterBlockEntityBehaviors(ICoreAPI api)
    {
        api.RegisterBlockEntityBehaviorClass("BERingable", typeof(BEBehaviorRingable));
        api.RegisterBlockEntityBehaviorClass("BELightable", typeof(BEBehaviorLightable));
        api.RegisterBlockEntityBehaviorClass("BEAnimatable", typeof(RPVoiceChat.GameContent.BlockEntityBehavior.BEBehaviorAnimatable));
    }

    public static void AddBlockEntityBehaviors(ICoreAPI api)
    {
        foreach (Block block in api.World.Blocks)
        {
            // Skip null or malformed blocks
            if (block == null || block.Code == null) continue;

            // Target only blocks that have the Door or TrapDoor behaviors
            bool isDoor = block.BlockBehaviors?.Any(behav => behav.GetType().Name == "BlockBehaviorDoor") == true;
            bool isTrapDoor = block.BlockBehaviors?.Any(behav => behav.GetType().Name == "BlockBehaviorTrapDoor") == true;

            if (!isDoor && !isTrapDoor) continue;

            // Skip if the block already has the BERingable behavior (prevent double injection)
            if (block.BlockEntityBehaviors?.Any(b => b.Name == "BERingable") == true) continue;

            // Clone existing behaviors or start with an empty list
            var newBehaviors = (block.BlockEntityBehaviors ?? new BlockEntityBehaviorType[0]).ToList();

            // Prepare custom properties (add whatever extra config you need here)
            var jsonProps = new JsonObject(new JObject());
            if (block.Attributes?["bellPartCode"]?.Exists == true)
            {
                jsonProps.Token["bellPartCode"] = block.Attributes["bellPartCode"].AsString();
            }

            // Create the new behavior type entry
            var ringableBehavior = new BlockEntityBehaviorType()
            {
                Name = "BERingable",
                properties = jsonProps
            };

            // Add your behavior to the end of the list (to avoid initialization order issues)
            newBehaviors.Add(ringableBehavior);
            block.BlockEntityBehaviors = newBehaviors.ToArray();

            // Important: do NOT override the EntityClass unless absolutely required.
            // Many vanilla blocks (like doors) have logic that depends on their specific BlockEntity (like BEBehaviorDoorBarLock) type.
            // So we leave it untouched unless you're injecting into a purely generic block.
        }
    }


}
