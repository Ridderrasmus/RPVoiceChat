using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace RPVoiceChat.Util
{
    public static class UIUtils
    {
        public static string I18n(string key)
        {
            return Lang.Get($"{RPVoiceChatMod.modID}:{key}");
        }

        public static string I18n(string key, params object[] args)
        {
            return Lang.Get($"{RPVoiceChatMod.modID}:{key}", args);
        }

        public static string TrimText(string text, double width, CairoFont font)
        {
            var suffix = "...";
            var suffixWidth = font.GetTextExtents(suffix).Width;
            var targetWidth = width - suffixWidth;

            var textWidth = font.GetTextExtents(text).Width;
            if (textWidth <= width) return text;
            var cutLocation = (int)(Math.Min(targetWidth / textWidth, 1) * text.Length);

            var result = $"{text.Substring(0, cutLocation)}{suffix}";

            return result;
        }
    }
}
