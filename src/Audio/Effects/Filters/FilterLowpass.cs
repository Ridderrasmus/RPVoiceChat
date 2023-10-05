namespace RPVoiceChat.Audio.Effects
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
            nullFilter = OALCW.EFX.GenFilter();
            GenerateFilter();
        }

        /// <summary>
        /// Sets the gain at the high-frequency limit of the filter. <br/>
        /// 1.0 means no change.
        /// </summary>
        /// <param name="gain">The gain from 0.0 to 1.0.</param>
        public void SetHFGain(float gain)
        {
            OALCW.EFX.Filter(filter, FilterFloat.LowpassGainHF, gain);
        }

        public void SetGain(float gain)
        {
            OALCW.EFX.Filter(filter, FilterFloat.LowpassGain, gain);
        }

        public void Start()
        {
            if (IsEnabled)
                return;

            OALW.Source(source, ALSourcei.EfxDirectFilter, filter);
            IsEnabled = true;
        }

        public void Stop()
        {
            if (!IsEnabled)
                return;

            OALW.Source(source, ALSourcei.EfxDirectFilter, nullFilter);
            IsEnabled = false;
        }

        private void GenerateFilter()
        {
            filter = OALCW.EFX.GenFilter();
            OALCW.EFX.Filter(filter, FilterInteger.FilterType, (int)FilterType.Lowpass);
            OALCW.EFX.Filter(filter, FilterFloat.LowpassGain, 1.0f);
            OALCW.EFX.Filter(filter, FilterFloat.LowpassGainHF, 0.2f);
        }

    }
}
