using OpenTK.Audio.OpenAL;
using System;

namespace RPVoiceChat.Audio
{
    public interface IAudioCapture : IDisposable
    {
        public int AvailableSamples { get; }
        public string CurrentDevice { get; }
        public ALFormat SampleFormat { get; }
        public void Start();
        public void Stop();
        public void ReadSamples(byte[] buffer, int count);
    }
}
