using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace RPVoiceChat.Utils
{
    public class RecipeHandler
    {
        private ICoreAPI api;
        private string modId;

        public RecipeHandler(ICoreAPI api, string modId)
        {
            this.api = api;
            this.modId = modId;
        }

        public void DisableRecipes()
        {
            var patches = new List<JsonPatch>();
            var recipes = new List<dynamic>()
                .Concat(api.GetSmithingRecipes())
                .Concat(api.GetClayformingRecipes())
                .Concat(api.GetKnappingRecipes())
                .Concat(api.GetBarrelRecipes());

            foreach (dynamic recipe in recipes)
            {
                string recipeDomain = recipe.Name.Domain;
                if (recipeDomain != modId) continue;

                recipe.Enabled = false;

                JsonItemStack outputItem = recipe.Output;
                //HideFromHandbook(outputItem.Code, ref outputItem.Attributes);
                patches.AddRange(CreatePatches(outputItem.Code, outputItem.Type));
                foreach (CraftingRecipeIngredient ingredient in recipe.Ingredients)
                    //HideFromHandbook(ingredient.Code, ref ingredient.Attributes);
                    patches.AddRange(CreatePatches(ingredient.Code, ingredient.Type));

                Logger.server.Notification($"Disabled recipe: {recipe.Name}");
            }

            foreach (GridRecipe recipe in api.World.GridRecipes)
            {
                string recipeDomain = recipe.Name.Domain;
                if (recipeDomain != modId) continue;

                recipe.Enabled = false;

                var outputItem = recipe.Output;
                //HideFromHandbook(outputItem.Code, ref outputItem.Attributes);
                patches.AddRange(CreatePatches(outputItem.Code, outputItem.Type));
                foreach (var ingredient in recipe.Ingredients)
                    patches.AddRange(CreatePatches(ingredient.Value.Code, ingredient.Value.Type));
                    //HideFromHandbook(ingredient.Value.Code, ref ingredient.Value.Attributes);

                Logger.server.Notification($"Disabled recipe: {recipe.Name}");
            }

            ApplyPatches(patches);
        }

        public void HideFromHandbook(AssetLocation code, ref JsonObject attributes)
        {
            if (code.Domain != modId) return;
            if (attributes == null)
            {
                attributes = JsonObject.FromJson("{ handbook: { exclude: true } }");
                return;
            }

            var root = attributes.Token as JObject;
            if (attributes["handbook"].Exists == false)
            {
                root.Add("handbook", JToken.Parse("{ exclude: true }"));
                attributes = new JsonObject(root);
                return;
            }

            var handbook = root["handbook"] as JObject;
            if (attributes["handbook"]["exclude"].Exists == false)
            {
                handbook.Add("exclude", true);
                attributes = new JsonObject(root);
                return;
            }

            handbook.Remove("exclude");
            handbook.Add("exclude", true);
            attributes = new JsonObject(root);
        }

        private List<JsonPatch> CreatePatches(AssetLocation code, EnumItemClass type)
        {
            List<JsonPatch> patches = new List<JsonPatch>();
            if (code.Domain != modId) return patches;

            var category = type == EnumItemClass.Item ? AssetCategory.itemtypes : AssetCategory.blocktypes;
            patches.Add(new JsonPatch()
            {
                Op = EnumJsonPatchOp.Add,
                Value = new JsonObject(true),
                Path = "/attributes/handbook/exclude",
                File = new AssetLocation($"{code.Domain}:{category}/{code.Path}")
            });
            patches.Add(new JsonPatch()
            {
                Op = EnumJsonPatchOp.Replace,
                Value = new JsonObject(true),
                Path = "/enabled",
                File = new AssetLocation($"{code.Domain}:{category}/{code.Path}")
            });
            patches.Add(new JsonPatch()
            {
                Op = EnumJsonPatchOp.Add,
                Value = new JsonObject(true),
                Path = "/attributes/handbook/exclude",
                File = new AssetLocation($"{code.Domain}:{category}/{code.Path}")
            });
            patches.Add(new JsonPatch()
            {
                Op = EnumJsonPatchOp.Replace,
                Value = new JsonObject(true),
                Path = "/attributes/handbook/exclude",
                File = new AssetLocation($"{code.Domain}:{category}/{code.Path}")
            });

            return patches;
        }

        private void ApplyPatches(List<JsonPatch> patches)
        {
            if (patches == null) return;

            var patchLoader = api.ModLoader.GetModSystem<ModJsonPatchLoader>();
            int appliedCount = 0;
            int notfoundCount = 0;
            int errorCount = 0;
            
            for (int i = 0; i < patches.Count; i++)
            {
                JsonPatch patch = patches[i];
                patchLoader.ApplyPatch(i, new AssetLocation(nameof(RPVoiceChatMod)), patch, ref appliedCount, ref notfoundCount, ref errorCount);
            }
        }
    }
}
