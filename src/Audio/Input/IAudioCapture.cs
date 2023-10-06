using System;
using System.Collections.Generic;

namespace RPVoiceChat.Audio
{
    public interface IAudioCapture : IDisposable
    {
        public int AvailableSamples { get; }
        public string CurrentDevice { get; }
        public static string DefaultDevice { get; }
        public ALFormat SampleFormat { get; }
        public void Start();
        public void Stop();
        public void ReadSamples(byte[] buffer, int count);
        public static abstract List<string> GetAvailableDevices();
    }
}
