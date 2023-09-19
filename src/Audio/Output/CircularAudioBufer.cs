using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Util;

namespace RPVoiceChat.Audio
{
    public class CircularAudioBuffer : IDisposable
    {
        private Thread dequeueAudioThread;
        private CancellationTokenSource dequeueAudioCTS;
        private List<int> availableBuffers = new List<int>();
        private List<int> queuedBuffers = new List<int>();
        private int[] buffers;
        private int source;
        private object dequeue_buffers_lock = new object();

        public CircularAudioBuffer(int source, int bufferCount)
        {
            this.source = source;
            buffers = OALW.GenBuffers(bufferCount);
            availableBuffers.AddRange(buffers);

            dequeueAudioThread = new Thread(DequeueAudio);
            dequeueAudioCTS = new CancellationTokenSource();
            dequeueAudioThread.Start(dequeueAudioCTS.Token);
        }

        public void QueueAudio(byte[] audio, int length, ALFormat format, int frequency)
        {
            // we arent playing back audio fast enough, better to skip the audio
            if (availableBuffers.Count == 0)
            {
                Logger.client.Debug("CircularAudioBuffer had to skip queuing audio");
                return;
            }

            var currentBuffer = availableBuffers.PopOne();

            OALW.ClearError();
            OALW.BufferData(currentBuffer, format, audio, frequency);
            OALW.SourceQueueBuffer(source, currentBuffer);
            queuedBuffers.Add(currentBuffer);
        }

        private void DequeueAudio(object cancellationToken)
        {
            CancellationToken ct = (CancellationToken)cancellationToken;
            while (dequeueAudioThread.IsAlive && !ct.IsCancellationRequested)
            {
                TryDequeueBuffers();
                Thread.Sleep(30);
            }
        }

        public void TryDequeueBuffers()
        {
            if (queuedBuffers.Count == 0) return;

            lock (dequeue_buffers_lock)
            {
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
        }

        public void Dispose()
        {
            dequeueAudioCTS?.Cancel();
            dequeueAudioCTS?.Dispose();
            OALW.DeleteBuffers(buffers);
        }
    }
}
