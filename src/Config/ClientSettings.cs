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

        public static int GetInt(string key)
        {
            return capi.Settings.Int[Key(key)];
        }

        public static bool GetBool(string key)
        {
            return capi.Settings.Bool[Key(key)];
        }

        public static float GetFloat(string key)
        {
            return capi.Settings.Float[Key(key)];
        }

        public static string GetStr(string key)
        {
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
