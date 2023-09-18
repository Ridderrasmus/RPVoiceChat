using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private ICoreAPI api;
        private RPVoiceChatConfig config;

        private IAsset recipeDict;

        public AssetLocation recipeDictionaryPath = new AssetLocation("rpvoicechat:config/recipedictionary.json");

        // Temporary hardcoded dictionary of recipes to disable (Should be replaced with json file defined above)
        private Dictionary<string, string[]> recipeDictionary = new Dictionary<string, string[]>() {
            { "item-callbell", new string[] { "callbell" } },
            { "item-handbell", new string[] { "callbell", "handbell" } },
            { "item-royalhorn", new string[] { "royalhorn", "royalhornheadtemp", "royalhornhandle" } },
            { "item-royalhornhead", new string[] { "royalhornhead" } },
            { "item-royalhornhandle", new string[] { "royalhornhandle" } }
        };

        public List<string> DisabledRecipes { get; private set; } = new List<string>();

        public RecipeHandler(ICoreAPI api)
        {
            this.api = api;
            this.config = ModConfig.Config;

        }

        public void DisableRecipes()
        {
            // Temporarily ditched this because it's not working
            recipeDict = api.Assets.TryGet(recipeDictionaryPath);
            if (recipeDict == null)
            {
                Logger.server.Error("Recipe dictionary not found at " + recipeDictionaryPath);
                return;
            }
            else
            {
                Logger.server.Debug("Recipe dictionary found at " + recipeDictionaryPath);
            }

            foreach (string itemToDisable in config.DisabledRecipes)
            {
                string[] recipes = GetRecipesFromCode(itemToDisable);

                if (recipes == null) continue;

                foreach (string recipe in recipes)
                    DisabledRecipes.Add(recipe + ".json");
            };

            foreach (SmithingRecipe recipe in api.GetSmithingRecipes())
            {
                string recipeName = recipe.Name.GetName();
                if (DisabledRecipes.Contains(recipeName))
                {
                    api.Logger.Notification("Disabled smithing recipe: " + recipeName);
                    recipe.Enabled = false;
                }
            }

            foreach (ClayFormingRecipe recipe in api.GetClayformingRecipes())
            {
                string recipeName = recipe.Name.GetName();
                if (DisabledRecipes.Contains(recipeName))
                {
                    api.Logger.Notification("Disabled clayforming recipe: " + recipeName);
                    recipe.Enabled = false;
                }
            }

            foreach (KnappingRecipe recipe in api.GetKnappingRecipes())
            {
                string recipeName = recipe.Name.GetName();
                if (DisabledRecipes.Contains(recipeName))
                {
                    api.Logger.Notification("Disabled knapping recipe: " + recipeName);
                    recipe.Enabled = false;
                }
            }

            foreach (BarrelRecipe recipe in api.GetBarrelRecipes())
            {
                string recipeName = recipe.Name.GetName();
                if (DisabledRecipes.Contains(recipeName))
                {
                    api.Logger.Notification("Disabled barrel recipe: " + recipeName);
                    recipe.Enabled = false;
                }
            }

            foreach (GridRecipe recipe in api.World.GridRecipes)
            {
                string recipeName = recipe.Name.GetName();
                if (DisabledRecipes.Contains(recipeName))
                {
                    api.Logger.Notification("Disabled grid recipe: " + recipeName);
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
            if (code == "item-code") return null;


            if (recipeDictionary.ContainsKey(code))
            {
                return recipeDictionary[code];
            }
            else
            {
                return null;
            }

        }
    }
}
