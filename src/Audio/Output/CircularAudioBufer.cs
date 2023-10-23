using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Util;

namespace RPVoiceChat.Audio
{
    public class CircularAudioBuffer : IDisposable
    {
        public event Action OnEmptyingQueue;
        private CancellationTokenSource dequeueAudioCTS;
        private List<int> availableBuffers = new List<int>();
        private List<int> queuedBuffers = new List<int>();
        private int[] buffers;
        private int source;
        private ALSourceState previousSourceState = ALSourceState.Initial;
        private object buffer_queue_lock = new object();

        public CircularAudioBuffer(int source, int bufferCount)
        {
            this.source = source;
            buffers = OALW.GenBuffers(bufferCount);
            availableBuffers.AddRange(buffers);

            dequeueAudioCTS = new CancellationTokenSource();
            DequeueAudio(dequeueAudioCTS.Token);
        }

        public void QueueAudio(byte[] audio, ALFormat format, int frequency)
        {
            FreeProcessedBuffers();

            // we arent playing back audio fast enough, better to skip the audio
            if (availableBuffers.Count == 0)
            {
                Logger.client.Debug("CircularAudioBuffer had to skip queuing audio");
                return;
            }

            lock (buffer_queue_lock)
            {
                var currentBuffer = availableBuffers.PopOne();

                OALW.ClearError();
                OALW.BufferData(currentBuffer, format, audio, frequency);
                OALW.SourceQueueBuffer(source, currentBuffer);
                queuedBuffers.Add(currentBuffer);
            }
        }

        private async void DequeueAudio(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                FreeProcessedBuffers();
                await Task.Delay(30);
            }
        }

        private void FreeProcessedBuffers()
        {
            bool sourceHasStopped;
            lock (buffer_queue_lock)
            {
                var sourceState = OALW.GetSourceState(source);
                sourceHasStopped = sourceState == ALSourceState.Stopped;
                if (sourceHasStopped && sourceState == previousSourceState) return;
                // Source is Playing or just entered Stopped state
                previousSourceState = sourceState;

                TryDequeueBuffers();
            }

            if (sourceHasStopped) OnEmptyingQueue?.Invoke();
        }

        private void TryDequeueBuffers()
        {
            if (queuedBuffers.Count == 0) return;

            OALW.ClearError();
            OALW.GetSource(source, ALGetSourcei.BuffersProcessed, out var buffersProcessed);
            if (buffersProcessed == 0) return;

            var buffer = OALW.SourceUnqueueBuffer(source);
            while (buffer != 0)
            {
                queuedBuffers.Remove(buffer);
                availableBuffers.Add(buffer);
                buffer = OALW.SourceUnqueueBuffer(source);
            }
        }

        public void Dispose()
        {
            dequeueAudioCTS?.Cancel();
            dequeueAudioCTS?.Dispose();
            OALW.DeleteBuffers(buffers);
        }
    }
}
