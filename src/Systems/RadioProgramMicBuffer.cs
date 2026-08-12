using System;
using System.Collections.Generic;
using RPVoiceChat.Audio.Input;

namespace RPVoiceChat.Systems
{
    public sealed class RadioProgramMicBuffer
    {
        private readonly Queue<short> samples = new();
        private readonly object sync = new();

        public void EnqueueSamples(IReadOnlyList<short> pcm)
        {
            if (pcm == null || pcm.Count == 0)
            {
                return;
            }

            lock (sync)
            {
                for (int i = 0; i < pcm.Count; i++)
                {
                    samples.Enqueue(pcm[i]);
                }

                const int maxBufferedSamples = RadioHlsStreamCapture.SampleRate * 2;
                while (samples.Count > maxBufferedSamples)
                {
                    samples.Dequeue();
                }
            }
        }

        public void ReadFrame(int frameSamples, Span<short> destination)
        {
            destination.Clear();
            if (frameSamples <= 0)
            {
                return;
            }

            lock (sync)
            {
                int toRead = Math.Min(frameSamples, samples.Count);
                for (int i = 0; i < toRead; i++)
                {
                    destination[i] = samples.Dequeue();
                }
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                samples.Clear();
            }
        }
    }
}
