using System.Collections.Generic;
using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Systems
{
    /// <summary>
    /// Ensures transmit frequencies (station consoles + repeater emitters) stay unique.
    /// Station consoles are checked while loaded; repeaters via world-level RF presence.
    /// Receivers may still freely tune to any frequency.
    /// </summary>
    public static class RadioTransmitFrequencyGuard
    {
        public static bool IsFrequencyAvailable(IWorldAccessor world, string frequency, BlockPos excludePos)
        {
            string normalized = RadioFrequencyUtil.Normalize(frequency);
            if (normalized.Length == 0)
            {
                return true;
            }

            foreach (string claimed in EnumerateClaimedTransmitFrequencies(world, excludePos))
            {
                if (RadioFrequencyUtil.Matches(claimed, normalized))
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<string> EnumerateClaimedTransmitFrequencies(IWorldAccessor world, BlockPos excludePos)
        {
            foreach (BlockEntityRadioSupervisionConsole console in RadioBlockIndex.GetLoadedSupervisionConsoles(world))
            {
                if (excludePos != null && console.Pos.Equals(excludePos))
                {
                    continue;
                }

                string frequency = RadioFrequencyUtil.Normalize(console.Frequency);
                if (frequency.Length > 0)
                {
                    yield return frequency;
                }
            }

            foreach (string frequency in RadioRfPresenceRegistry.EnumerateClaimedTransmitFrequencies(excludePos))
            {
                yield return frequency;
            }
        }
    }
}
