using System;

namespace RPVoiceChat.Systems
{
    public static class RadioFrequencyUtil
    {
        public static string Normalize(string frequency)
        {
            if (string.IsNullOrWhiteSpace(frequency))
            {
                return "";
            }

            return frequency.Trim();
        }

        public static bool Matches(string a, string b)
        {
            string left = Normalize(a);
            string right = Normalize(b);
            if (left.Length == 0 || right.Length == 0)
            {
                return false;
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
