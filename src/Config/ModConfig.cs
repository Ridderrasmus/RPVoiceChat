using Vintagestory.API.Common;

namespace RPVoiceChat
{
    static class ModConfig
    {
        private const string ConfigFileName = "rpvoicechat.json";
        public static RPVoiceChatConfig Config { get; private set; }

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
                    Config = LoadConfig(api);
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
        }

        private static RPVoiceChatConfig LoadConfig(ICoreAPI api) => api.LoadModConfig<RPVoiceChatConfig>(ConfigFileName);
        private static void GenerateConfig(ICoreAPI api) => api.StoreModConfig(new RPVoiceChatConfig(), ConfigFileName);
        private static void GenerateConfig(ICoreAPI api, RPVoiceChatConfig previousConfig) => api.StoreModConfig(new RPVoiceChatConfig(api.Side, previousConfig), ConfigFileName);
    }
}
