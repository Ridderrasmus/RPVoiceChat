

using OpenTK.Audio.OpenAL;

namespace rpvoicechat.src.Utils.Filters
{
    public class FilterLowpass
    {

        private EffectsExtension effectsExtension;
        private int source;
        public int filter;

        public bool IsEnabled { get; set; } = false;


        public FilterLowpass(EffectsExtension effectsExtension, int source) 
        {
            this.source = source;
            this.effectsExtension = effectsExtension;
            GenerateFilter();
        }

        public void SetHFGain(float gain)
        // Summary:
        //     Sets the gain at the high-frequency limit of the filter.
        //     1.0 means no change.
        //     
        // Parameters:
        //   gain: The gain from 0.0 to 1.0.
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

            effectsExtension.DeleteFilter(filter);
            GenerateFilter();
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
