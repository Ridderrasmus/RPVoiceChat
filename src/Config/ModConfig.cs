using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace rpvoicechat
{
    static class ModConfig
    {
        private const string jsonConfig = "RPVoiceChatConfig.json";
        private static RPVoiceChatConfig config;

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

            foreach (var setting in config.Settings)
            {
                api.World.Config.SetString($"rpvoicechat:{setting.Key}", setting.Value);
            }
            
        }

        private static RPVoiceChatConfig LoadConfig(ICoreAPI api) => api.LoadModConfig<RPVoiceChatConfig>(jsonConfig);
        private static void GenerateConfig(ICoreAPI api) => api.StoreModConfig(new RPVoiceChatConfig(), jsonConfig);
        private static void GenerateConfig(ICoreAPI api, RPVoiceChatConfig prevConfig) => api.StoreModConfig(new RPVoiceChatConfig(prevConfig), jsonConfig);
        
    }
}
