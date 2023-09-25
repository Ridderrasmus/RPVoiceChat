using Vintagestory.API.Client;

namespace RPVoiceChat
{
    public static class ClientSettings
    {
        private const string modPrefix = "RPVoiceChat";
        private static ICoreClientAPI capi;

        public static void Init(ICoreClientAPI api)
        {
            capi = api;
        }

        public static void Set(string key, int value)
        {
            capi.Settings.Int[Key(key)] = value;
        }

        public static void Set(string key, bool value)
        {
            capi.Settings.Bool[Key(key)] = value;
        }

        public static void Set(string key, float value)
        {
            capi.Settings.Float[Key(key)] = value;
        }

        public static void Set(string key, string value)
        {
            capi.Settings.String[Key(key)] = value;
        }

        public static int GetInt(string key, int defaultValue)
        {
            return GetInt(key) ?? defaultValue;
        }

        public static int? GetInt(string key)
        {
            if (!capi.Settings.Int.Exists(Key(key))) return null;
            return capi.Settings.Int[Key(key)];
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            return GetBool(key) ?? defaultValue;
        }

        public static bool? GetBool(string key)
        {
            if (!capi.Settings.Bool.Exists(Key(key))) return null;
            return capi.Settings.Bool[Key(key)];
        }

        public static float GetFloat(string key, float defaultValue)
        {
            return GetFloat(key) ?? defaultValue;
        }

        public static float? GetFloat(string key)
        {
            if (!capi.Settings.Float.Exists(Key(key))) return null;
            return capi.Settings.Float[Key(key)];
        }

        public static string GetStr(string key, string defaultValue)
        {
            return GetStr(key) ?? defaultValue;
        }

        public static string GetStr(string key)
        {
            if (!capi.Settings.String.Exists(Key(key))) return null;
            return capi.Settings.String[Key(key)];
        }

        private static string Key(string key)
        {
            return $"{modPrefix}_{key}";
        }

        public static void Dispose()
        {
            capi = null;
        }
    }
}
