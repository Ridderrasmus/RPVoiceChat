using System;
using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio.Effects
{
    public class LowpassFilter : IDisposable
    {
        private readonly int source;
        public int filter;
        public bool IsEnabled { get; private set; }

        public LowpassFilter(int source)
        {
            this.source = source;
            var device = ALC.GetContextsDevice(ALC.GetCurrentContext());
            if (device == IntPtr.Zero || !ALC.IsExtensionPresent(device, "ALC_EXT_EFX")) return;
            OALW.ClearError();
            filter = ALC.EFX.GenFilter();
            if (AL.GetError() != ALError.NoError || filter == 0) { Dispose(); return; }
            ALC.EFX.Filter(filter, FilterInteger.FilterType, (int)FilterType.Lowpass);
            ALC.EFX.Filter(filter, FilterFloat.LowpassGain, 1f);
            ALC.EFX.Filter(filter, FilterFloat.LowpassGainHF, 0.2f);
            if (AL.GetError() != ALError.NoError) Dispose();
        }

        public void SetHFGain(float gain)
        {
            if (filter != 0) ALC.EFX.Filter(filter, FilterFloat.LowpassGainHF, Math.Clamp(gain, 0, 1));
        }

        public void SetGain(float gain)
        {
            if (filter != 0) ALC.EFX.Filter(filter, FilterFloat.LowpassGain, Math.Clamp(gain, 0, 1));
        }

        public void Start()
        {
            if (filter == 0 || IsEnabled) return;
            AL.Source(source, ALSourcei.EfxDirectFilter, filter);
            IsEnabled = true;
        }

        public void Stop()
        {
            if (!IsEnabled) return;
            AL.Source(source, ALSourcei.EfxDirectFilter, 0);
            IsEnabled = false;
        }

        public void Dispose()
        {
            Stop();
            if (filter != 0) ALC.EFX.DeleteFilter(filter);
            filter = 0;
        }
    }
}
