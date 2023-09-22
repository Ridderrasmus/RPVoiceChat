﻿using System;

namespace RPVoiceChat.Audio
{
    public interface IDenoiser : IDisposable
    {
        public void Denoise(ref short[] pcms);
        public void SetBackgroundNoiseThreshold(float value);
        public void SetVoiceDenoisingStrength(float value);
        public bool SupportsFrequency(int frequency);
    }
}
