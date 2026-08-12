using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;

namespace RPVoiceChat.Systems
{
    public static class RadioStationNameResolver
    {
        /// <summary>
        /// Finds a non-empty station DisplayName for a frequency from a powered RF emitter
        /// that has a wired supervision console name.
        /// </summary>
        public static string Resolve(IWorldAccessor world, string frequency)
        {
            string tuned = RadioFrequencyUtil.Normalize(frequency);
            if (world?.BlockAccessor == null || tuned.Length == 0)
            {
                return "";
            }

            foreach (BlockEntityRadioEmitter emitter in RadioBlockIndex.GetLoadedEmitters(world))
            {
                if (emitter == null || !emitter.HasSufficientTransmitPower())
                {
                    continue;
                }

                string emitterFrequency = emitter.IsRepeaterMode
                    ? RadioFrequencyUtil.Normalize(emitter.RepeaterFrequency)
                    : RadioFrequencyUtil.Normalize(emitter.GetConsoleFrequency());

                if (!RadioFrequencyUtil.Matches(emitterFrequency, tuned))
                {
                    continue;
                }

                // Station name lives on the wired supervision console (source emitters).
                string name = emitter.GetConsoleDisplayName()?.Trim() ?? "";
                if (name.Length > 0)
                {
                    return name;
                }
            }

            return "";
        }
    }
}
