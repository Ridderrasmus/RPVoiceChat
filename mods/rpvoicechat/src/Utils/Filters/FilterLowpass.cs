

using OpenTK.Audio.OpenAL;

namespace rpvoicechat.src.Utils.Filters
{
    public class FilterLowpass
    {

        private EffectsExtension effectsExtension;
        private int source;
        private int filter;

        public bool IsEnabled { get; set; } = false;


        public FilterLowpass(EffectsExtension effectsExtension, int source) 
        {
            this.source = source;
            this.effectsExtension = effectsExtension;
            GenerateFilter();
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
            effectsExtension.Filter(filter, EfxFilterf.LowpassGainHF, 1.0f);
        }

    }
}
