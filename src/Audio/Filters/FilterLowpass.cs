using OpenTK.Audio.OpenAL;

namespace rpvoicechat
{
    public class FilterLowpass
    {

        private EffectsExtension effectsExtension;
        private int source;
        private int nullFilter;
        public int filter;

        public bool IsEnabled { get; set; } = false;


        public FilterLowpass(EffectsExtension effectsExtension, int source)
        {
            this.effectsExtension = effectsExtension;
            this.source = source;
            nullFilter = effectsExtension.GenFilter();
            GenerateFilter();
        }

        /// <summary>
        /// Sets the gain at the high-frequency limit of the filter. <br/>
        /// 1.0 means no change.
        /// </summary>
        /// <param name="gain">The gain from 0.0 to 1.0.</param>
        public void SetHFGain(float gain)
        {
            effectsExtension.Filter(filter, EfxFilterf.LowpassGainHF, gain);
        }

        public void SetGain(float gain)
        {
            effectsExtension.Filter(filter, EfxFilterf.LowpassGain, gain);
        }

        public void Start()
        {
            if (IsEnabled)
                return;

            effectsExtension.BindFilterToSource(source, filter);
            IsEnabled = true;
        }

        public void Stop()
        {
            if (!IsEnabled)
                return;

            effectsExtension.BindFilterToSource(source, nullFilter);
            IsEnabled = false;
        }

        private void GenerateFilter()
        {
            filter = effectsExtension.GenFilter();
            effectsExtension.Filter(filter, EfxFilteri.FilterType, (int)EfxFilterType.Lowpass);
            effectsExtension.Filter(filter, EfxFilterf.LowpassGain, 1.0f);
            effectsExtension.Filter(filter, EfxFilterf.LowpassGainHF, 0.2f);
        }

    }
}
