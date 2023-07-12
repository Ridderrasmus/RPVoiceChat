using Vintagestory.API.Common;
using Vintagestory.ServerMods;

namespace rpvoicechat
{
    static class ModConfig
    {
        
        private const string ConfigFileName = "rpvoicechat.json";
        public static RPVoiceChatConfig config { get; private set; }

        public static void ReadConfig(ICoreAPI api)
        {
            
            try
            {
                config = LoadConfig(api);

                if (config == null)
                {
                    GenerateConfig(api);
                    config = LoadConfig(api);
                }
                else
                {
                    GenerateConfig(api, config);
                }
            }
            catch
            {
                GenerateConfig(api);
                config = LoadConfig(api);
            }
        }

        public static void Save(ICoreAPI api)
        {
            GenerateConfig(api, config);
        }

        private static RPVoiceChatConfig LoadConfig(ICoreAPI api) => api.LoadModConfig<RPVoiceChatConfig>(ConfigFileName);
        private static void GenerateConfig(ICoreAPI api) => api.StoreModConfig(new RPVoiceChatConfig(), ConfigFileName);
        private static void GenerateConfig(ICoreAPI api, RPVoiceChatConfig previousConfig) => api.StoreModConfig(new RPVoiceChatConfig(previousConfig), ConfigFileName);
        
    }
}
