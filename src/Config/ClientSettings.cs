using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public static class ClientSettings
    {
        public static float OutputGain { get => GetFloat("outputGain", 1); set => Set("outputGain", value); }
        public static float InputGain { get => GetFloat("inputGain", 1); set => Set("inputGain", value); }
        public static float InputThreshold { get => GetFloat("inputThreshold", 0.2f); set => Set("inputThreshold", value); }
        public static float BackgroundNoiseThreshold { get => GetFloat("denoisingSensitivity", 0.5f); set => Set("denoisingSensitivity", value); }
        public static float VoiceDenoisingStrength { get => GetFloat("denoisingStrength", 0.1f); set => Set("denoisingStrength", value); }
        public static bool PushToTalkEnabled { get => GetBool("ptt", false); set => Set("ptt", value); }
        public static bool IsMuted { get => GetBool("muteMic", false); set => Set("muteMic", value); }
        public static bool Loopback { get => GetBool("loopback", false); set => Set("loopback", value); }
        public static bool ShowHud { get => GetBool("showHud", true); set => Set("showHud", value); }
        public static bool Muffling { get => GetBool("muffling", true); set => Set("muffling", value); }
        public static bool Denoising { get => GetBool("denoising", false); set => Set("denoising", value); }
        public static bool ChannelGuessing { get => GetBool("channelGuessing", true); set => Set("channelGuessing", value); }
        public static int ActiveConfigTab { get => GetInt("activeConfigTab", 0); set => Set("activeConfigTab", value); }
        public static string CurrentInputDevice { get => GetStr("inputDevice"); set => Set("inputDevice", value); }

        private const string modPrefix = "RPVoiceChat";
        private static ICoreClientAPI capi;

        public static void Init(ICoreAPI api)
        {
            if (api is ICoreClientAPI capi)
                ClientSettings.capi = capi;
        }

        public static void Save()
        {
            ((Vintagestory.Client.NoObf.ClientSettings)capi?.Settings)?.Save();
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
    }
}
