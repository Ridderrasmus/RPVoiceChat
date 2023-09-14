using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RPVoiceChat.Utils
{
    public class RecipeHandler
    {
        private ICoreServerAPI sapi;
        private RPVoiceChatConfig config;


        
        public AssetLocation recipeDictionaryPath = new AssetLocation("rpvoicechat:config/recipedictionary.json");

        public RecipeHandler(ICoreServerAPI sapi, RPVoiceChatConfig config)
        {
            this.sapi = sapi;
            this.config = config;
        }

        public void DisableGridRecipes()
        {
            List<string> disabledRecipes = new List<string>();

            foreach (string itemToDisable in config.DisabledRecipes)
            {
                string[] recipes = GetRecipesFromCode(itemToDisable);
                if (recipes != null)
                    foreach (string recipe in recipes)
                        if (recipe != "item-code")
                            disabledRecipes.Add(recipe);
            };


            foreach (GridRecipe recipe in sapi.World.GridRecipes)
            {
                string recipeName = recipe.Name.GetName();
                if (disabledRecipes.Contains(recipeName) && config.DisabledRecipes.Contains(recipeName))
                {
                    Logger.server.Notification("Disabled recipe: " + recipeName);
                    recipe.Enabled = false;
                }
            }

            foreach (SmithingRecipe recipe in sapi.GetSmithingRecipes())
            {
                string recipeName = recipe.Name.GetName();
                if (disabledRecipes.Contains(recipeName) && config.DisabledRecipes.Contains(recipeName))
                {
                    Logger.server.Notification("Disabled recipe: " + recipeName);
                    recipe.Enabled = false;
                }
            }

            foreach (ClayFormingRecipe recipe in sapi.GetClayformingRecipes())
            {
                string recipeName = recipe.Name.GetName();
                if (disabledRecipes.Contains(recipeName) && config.DisabledRecipes.Contains(recipeName))
                {
                    Logger.server.Notification("Disabled recipe: " + recipeName);
                    recipe.Enabled = false;
                }
            }

            foreach (KnappingRecipe recipe in sapi.GetKnappingRecipes())
            {
                string recipeName = recipe.Name.GetName();
                if (disabledRecipes.Contains(recipeName) && config.DisabledRecipes.Contains(recipeName))
                {
                    Logger.server.Notification("Disabled recipe: " + recipeName);
                    recipe.Enabled = false;
                }
            }

            foreach (BarrelRecipe recipe in sapi.GetBarrelRecipes())
            {
                string recipeName = recipe.Name.GetName();
                if (disabledRecipes.Contains(recipeName) && config.DisabledRecipes.Contains(recipeName))
                {
                    Logger.server.Notification("Disabled recipe: " + recipeName);
                    recipe.Enabled = false;
                }
            }
        }

        /// <summary>
        /// Gets the recipes used to craft an item from its code
        /// </summary>
        /// <param name="code">The item code of what you want to gather the recipes for</param>
        /// <returns></returns>
        private string[] GetRecipesFromCode(string code)
        {
            IAsset recipeDict = sapi.Assets.TryGet(recipeDictionaryPath);
            JsonObject jsonrecipeDict = recipeDict.ToObject<JsonObject>();


            if (jsonrecipeDict.KeyExists(code))
            {
                return jsonrecipeDict[code].AsArray<string>();
            }
            else
            {
                return null;
            }
        }
    }
}
