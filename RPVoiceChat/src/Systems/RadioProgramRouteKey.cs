using Vintagestory.API.MathTools;

namespace RPVoiceChat.Systems
{
    public static class RadioProgramRouteKey
    {
        public const string Prefix = "rpvc:program:";

        public static string ForMixingConsole(BlockPos pos)
        {
            if (pos == null)
            {
                return "";
            }

            return $"{Prefix}{pos.X}:{pos.Y}:{pos.Z}:{pos.dimension}";
        }

        public static bool IsProgramSource(string playerOrSourceId)
        {
            return !string.IsNullOrWhiteSpace(playerOrSourceId)
                && playerOrSourceId.StartsWith(Prefix);
        }
    }
}
