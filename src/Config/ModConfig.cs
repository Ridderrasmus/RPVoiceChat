using System;
using Vintagestory.API.Common;

namespace rpvoicechat
{
    static class ModConfig
    {
        
        private const string ConfigFileName = "rpvoicechat.json";
        public static RPVoiceChatConfig Config { get; private set; }
        public static event Action ConfigUpdated;

        public static void ReadConfig(ICoreAPI api)
        {
            
            try
            {
                Config = LoadConfig(api);

                if (Config == null)
                {
                    GenerateConfig(api);
                    Config = LoadConfig(api);
                }
                else
                {
                    GenerateConfig(api, Config);
                }
            }
            catch
            {
                GenerateConfig(api);
                Config = LoadConfig(api);
            }
        }

        public static void Save(ICoreAPI api)
        {
            GenerateConfig(api, Config);
            ConfigUpdated?.Invoke();
        }

        private static RPVoiceChatConfig LoadConfig(ICoreAPI api) => api.LoadModConfig<RPVoiceChatConfig>(ConfigFileName);
        private static void GenerateConfig(ICoreAPI api) => api.StoreModConfig(new RPVoiceChatConfig(), ConfigFileName);
        private static void GenerateConfig(ICoreAPI api, RPVoiceChatConfig previousConfig) => api.StoreModConfig(new RPVoiceChatConfig(previousConfig), ConfigFileName);
        
    }
}
