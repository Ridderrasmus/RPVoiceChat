using OpenTK.Audio.OpenAL;

namespace RPVoiceChat
{
    public class FilterLowpass
    {
        private int source;
        private int nullFilter;
        public int filter;

        public bool IsEnabled { get; set; } = false;


        public FilterLowpass(int source)
        {
            this.source = source;
            nullFilter = ALC.EFX.GenFilter();
            GenerateFilter();
        }

        /// <summary>
        /// Sets the gain at the high-frequency limit of the filter. <br/>
        /// 1.0 means no change.
        /// </summary>
        /// <param name="gain">The gain from 0.0 to 1.0.</param>
        public void SetHFGain(float gain)
        {
            ALC.EFX.Filter(filter, FilterFloat.LowpassGainHF, gain);
        }

        public void SetGain(float gain)
        {
            ALC.EFX.Filter(filter, FilterFloat.LowpassGain, gain);
        }

        public void Start()
        {
            if (IsEnabled)
                return;

            AL.Source(source, ALSourcei.EfxDirectFilter, filter);
            IsEnabled = true;
        }

        public void Stop()
        {
            if (!IsEnabled)
                return;

            AL.Source(source, ALSourcei.EfxDirectFilter, nullFilter);
            IsEnabled = false;
        }

        private void GenerateFilter()
        {
            filter = ALC.EFX.GenFilter();
            ALC.EFX.Filter(filter, FilterInteger.FilterType, (int)FilterType.Lowpass);
            ALC.EFX.Filter(filter, FilterFloat.LowpassGain, 1.0f);
            ALC.EFX.Filter(filter, FilterFloat.LowpassGainHF, 0.2f);
        }

    }
}
