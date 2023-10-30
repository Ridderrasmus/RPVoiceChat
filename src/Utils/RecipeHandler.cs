using Vintagestory.API.Common;

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
            foreach (GridRecipe recipe in api.World.GridRecipes)
            {
                string recipeDomain = recipe.Name.Domain;
                if (recipeDomain != modId) continue;

                recipe.Enabled = false;

                Logger.server.Notification($"Disabled recipe: {recipe.Name}");
            }
        }
    }
}
