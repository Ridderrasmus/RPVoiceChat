using System;
using Vintagestory.API.Common;

namespace RPVoiceChat.Config
{
    public static class ModConfig
    {
        public const string ClientConfigName = "rpvoicechat-client.json";
        public const string ServerConfigName = "rpvoicechat-server.json";

        public static RPVoiceChatClientConfig ClientConfig { get; private set; }
        public static RPVoiceChatServerConfig ServerConfig { get; private set; }

        public static void Init(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client)
            {
                ClientConfig = ReadConfig<RPVoiceChatClientConfig>(api, ClientConfigName);
            }
            else
            {
                ServerConfig = ReadConfig<RPVoiceChatServerConfig>(api, ServerConfigName);
            }
        }

        public static void SaveClient(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client && ClientConfig != null)
            {
                WriteConfig(api, ClientConfigName, ClientConfig);
            }
        }

        public static void SaveServer(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Server && ServerConfig != null)
            {
                WriteConfig(api, ServerConfigName, ServerConfig);
            }
        }

        private static T ReadConfig<T>(ICoreAPI api, string jsonConfig) where T : class, IModConfig
        {
            T config;

            try
            {
                config = LoadConfig<T>(api, jsonConfig);

                if (config == null)
                {
                    api.Logger.Notification($"[RPVoiceChat] No {typeof(T).Name} found, generating default one.");
                    GenerateConfig<T>(api, jsonConfig);
                    config = LoadConfig<T>(api, jsonConfig);
                }
                else
                {
                    GenerateConfig(api, jsonConfig, config);
                    config = LoadConfig<T>(api, jsonConfig);
                }
            }
            catch (Exception e)
            {
                api.Logger.Warning($"[RPVoiceChat] Error loading {typeof(T).Name}: {e.Message}");
                api.Logger.Warning("[RPVoiceChat] Generating default config.");
                GenerateConfig<T>(api, jsonConfig);
                config = LoadConfig<T>(api, jsonConfig);
            }

            return config;
        }

        private static void WriteConfig<T>(ICoreAPI api, string jsonConfig, T config) where T : class, IModConfig
        {
            GenerateConfig(api, jsonConfig, config);
        }

        private static T LoadConfig<T>(ICoreAPI api, string jsonConfig) where T : class, IModConfig
        {
            return api.LoadModConfig<T>(jsonConfig);
        }

        private static void GenerateConfig<T>(ICoreAPI api, string jsonConfig, T previousConfig = null) where T : class, IModConfig
        {
            api.StoreModConfig(CloneConfig<T>(api, previousConfig), jsonConfig);
        }

        private static T CloneConfig<T>(ICoreAPI api, T config = null) where T : class, IModConfig
        {
            return (T)Activator.CreateInstance(typeof(T), new object[] { api, config });
        }
    }
}