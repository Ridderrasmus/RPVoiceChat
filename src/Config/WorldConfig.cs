using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    static class WorldConfig
    {
        private const string modPrefix = "rpvoicechat";
        private static ICoreAPI api;
        private static Dictionary<VoiceLevel, string> configKeyByVoiceLevel = new Dictionary<VoiceLevel, string>
        {
            { VoiceLevel.Whispering, "distance-whisper" },
            { VoiceLevel.Talking, "distance-talk" },
            { VoiceLevel.Shouting, "distance-shout" },
        };

        public static void Init(ICoreAPI api)
        {
            WorldConfig.api = api;
        }

        public static void Set(VoiceLevel voiceLevel, int distance)
        {
            configKeyByVoiceLevel.TryGetValue(voiceLevel, out string key);

            Set(key, distance);
        }

        public static void Set(string key, int value)
        {
            api.World.Config.SetInt(Key(key), value);
        }

        public static void Set(string key, bool value)
        {
            api.World.Config.SetBool(Key(key), value);
        }

        public static void Set(string key, float value)
        {
            api.World.Config.SetFloat(Key(key), value);
        }

        public static void Set(string key, string value)
        {
            api.World.Config.SetString(Key(key), value);
        }

        public static int GetInt(VoiceLevel voiceLevel)
        {
            configKeyByVoiceLevel.TryGetValue(voiceLevel, out string key);

            return GetInt(key, (int)voiceLevel);
        }

        public static int GetInt(string key, int? defaultValue = null)
        {
            if (defaultValue != null) return api.World.Config.GetInt(Key(key), (int)defaultValue);
            return api.World.Config.GetInt(Key(key));
        }

        public static bool GetBool(string key, bool? defaultValue = null)
        {
            if (defaultValue != null) return api.World.Config.GetBool(Key(key), (bool)defaultValue);
            return api.World.Config.GetBool(Key(key));
        }

        public static float GetFloat(string key, float? defaultValue = null)
        {
            if (defaultValue != null) return api.World.Config.GetFloat(Key(key), (float)defaultValue);
            return api.World.Config.GetFloat(Key(key));
        }

        public static string GetString(string key, string defaultValue = null)
        {
            return api.World.Config.GetString(Key(key), defaultValue);
        }

        private static string Key(string key)
        {
            return $"{modPrefix}:{key}";
        }
    }
}
