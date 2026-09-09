using System;

namespace RPVoiceChat.Audio.Effects
{
    public interface IDenoiser : IDisposable
    {
        public void Denoise(ref short[] pcms);
        public void SetBackgroundNoiseThreshold(float value);
        public void SetVoiceDenoisingStrength(float value);
        public bool SupportsFormat(int frequency, int channels, int bits);
    }
}
