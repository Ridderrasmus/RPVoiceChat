using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RPVoiceChat.Config
{
    static class WorldConfig
    {
        private const string modPrefix = "rpvoicechat";
        private static ICoreAPI api;

        private static readonly Dictionary<VoiceLevel, string> configKeyByVoiceLevel = new Dictionary<VoiceLevel, string>
        {
            { VoiceLevel.Whispering, "distance-whisper" },
            { VoiceLevel.Talking, "distance-talk" },
            { VoiceLevel.Shouting, "distance-shout" },
        };

        public static void Init(ICoreAPI api)
        {
            if (WorldConfig.api != null && api.Side == EnumAppSide.Client) return;
            WorldConfig.api = api;
        }

        public static void Set(VoiceLevel voiceLevel, int distance)
        {
            if (configKeyByVoiceLevel.TryGetValue(voiceLevel, out string key))
            {
                Set(key, distance);
            }
        }

        public static void Set(string key, int value)
        {
            api?.World.Config.SetInt(Key(key), value);
        }

        public static void Set(string key, bool value)
        {
            api?.World.Config.SetBool(Key(key), value);
        }

        public static void Set(string key, float value)
        {
            api?.World.Config.SetFloat(Key(key), value);
        }

        public static void Set(string key, string value)
        {
            api?.World.Config.SetString(Key(key), value);
        }

        public static int GetInt(VoiceLevel voiceLevel, int? defaultValue = null)
        {
            if (configKeyByVoiceLevel.TryGetValue(voiceLevel, out string key))
            {
                int fallbackDefault = defaultValue ?? (int)voiceLevel;
                return GetInt(key, fallbackDefault);
            }
            return defaultValue ?? (int)voiceLevel;
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            if (api?.World.Config.HasAttribute(Key(key)) == true)
            {
                return api.World.Config.GetInt(Key(key));
            }
            return defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            if (api?.World.Config.HasAttribute(Key(key)) == true)
            {
                return api.World.Config.GetBool(Key(key));
            }
            return defaultValue;
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
            if (api?.World.Config.HasAttribute(Key(key)) == true)
            {
                return api.World.Config.GetFloat(Key(key));
            }
            return defaultValue;
        }

        public static string GetString(string key, string defaultValue = null)
        {
            if (api?.World.Config.HasAttribute(Key(key)) == true)
            {
                return api.World.Config.GetString(Key(key));
            }
            return defaultValue;
        }

        private static string Key(string key)
        {
            return $"{modPrefix}:{key}";
        }
    }
}