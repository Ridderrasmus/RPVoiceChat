using System;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace RPVoiceChat.Config
{
    public static class ModConfig
    {
        private const string ConfigSubfolder = "RPVoiceChat";
        public const string ClientConfigName = "rpvoicechat-client.json";
        public const string ServerConfigName = "rpvoicechat-server.json";

        public static RPVoiceChatClientConfig ClientConfig { get; private set; }
        public static RPVoiceChatServerConfig ServerConfig { get; private set; }

        private static string GetConfigDirectory()
        {
            string configPath = GamePaths.ModConfig;
            return Path.Combine(configPath, ConfigSubfolder);
        }

        private static void EnsureConfigDirectoryExists()
        {
            try
            {
                string configDir = GetConfigDirectory();
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
            }
            catch (Exception)
            {
                // Log will be done in the calling method if api is available
            }
        }

        public static void Init(ICoreAPI api)
        {
            EnsureConfigDirectoryExists();

            if (api.Side == EnumAppSide.Client)
            {
                ClientConfig = ReadConfig<RPVoiceChatClientConfig>(api, ClientConfigName);
                // Also load server config on client side for shared settings (needed for singleplayer)
                ServerConfig = ReadConfig<RPVoiceChatServerConfig>(api, ServerConfigName);
                // Validate server configuration after loading
                ServerConfigManager.ValidateConfig();
            }
            else
            {
                ServerConfig = ReadConfig<RPVoiceChatServerConfig>(api, ServerConfigName);
                // Validate server configuration after loading
                ServerConfigManager.ValidateConfig();
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
                    // Config loaded successfully, no migration needed
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
            try
            {
                GenerateConfig(api, jsonConfig, config);
            }
            catch (Exception e)
            {
                api.Logger.Error($"[RPVoiceChat] Failed to save {typeof(T).Name}: {e.Message}");
            }
        }

        private static T LoadConfig<T>(ICoreAPI api, string jsonConfig) where T : class, IModConfig
        {
            try
            {
                string configPath = Path.Combine(GetConfigDirectory(), jsonConfig);
                
                // Try loading from subdirectory first
                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    return JsonConvert.DeserializeObject<T>(jsonContent);
                }

                // Fallback: try loading from old location for migration
                try
                {
                    T oldConfig = api.LoadModConfig<T>(jsonConfig);
                    if (oldConfig != null)
                    {
                        // Migrate to new location
                        api.Logger.Notification($"[RPVoiceChat] Migrating {jsonConfig} to new location in {ConfigSubfolder} folder");
                        GenerateConfig(api, jsonConfig, oldConfig);
                        
                        // Delete old config file after successful migration
                        try
                        {
                            string oldConfigPath = Path.Combine(GamePaths.ModConfig, jsonConfig);
                            if (File.Exists(oldConfigPath))
                            {
                                File.Delete(oldConfigPath);
                                api.Logger.Notification($"[RPVoiceChat] Deleted old config file: {oldConfigPath}");
                            }
                        }
                        catch (Exception deleteEx)
                        {
                            api.Logger.Warning($"[RPVoiceChat] Failed to delete old config file: {deleteEx.Message}");
                        }
                        
                        return oldConfig;
                    }
                }
                catch
                {
                    // Old config doesn't exist, continue
                }

                return null;
            }
            catch (Exception e)
            {
                api.Logger.Warning($"[RPVoiceChat] Failed to load {jsonConfig}: {e.Message}");
                return null;
            }
        }

        private static void GenerateConfig<T>(ICoreAPI api, string jsonConfig, T previousConfig = null) where T : class, IModConfig
        {
            try
            {
                EnsureConfigDirectoryExists();
                var configToSave = CloneConfig<T>(api, previousConfig);
                
                string configPath = Path.Combine(GetConfigDirectory(), jsonConfig);
                string jsonContent = JsonConvert.SerializeObject(configToSave, Formatting.Indented);
                File.WriteAllText(configPath, jsonContent);
            }
            catch (Exception e)
            {
                api.Logger.Error($"[RPVoiceChat] Failed to generate {jsonConfig}: {e.Message}");
                throw;
            }
        }

        private static T CloneConfig<T>(ICoreAPI api, T config = null) where T : class, IModConfig
        {
            try
            {
                return (T)Activator.CreateInstance(typeof(T), new object[] { api, config });
            }
            catch (Exception e)
            {
                api.Logger.Error($"[RPVoiceChat] Failed to clone config {typeof(T).Name}: {e.Message}");
                // Fallback to default constructor
                return (T)Activator.CreateInstance(typeof(T));
            }
        }
    }
}