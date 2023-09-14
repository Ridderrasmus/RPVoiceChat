using System.Collections.Generic;

namespace RPVoiceChat.Utils
{
    public class RecipeHandler
    {

        public Dictionary<string, List<string>> itemCodeToRecipes = new Dictionary<string, List<string>>()
        {
            { "item-callbell", new List<string> { "callbell" }  }
        };
    }
}
