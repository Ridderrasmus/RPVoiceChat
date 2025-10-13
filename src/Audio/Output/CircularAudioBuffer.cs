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

        // Track the dequeue task to properly wait for it on disposal
        private Task dequeueTask;

        public CircularAudioBuffer(int source, int bufferCount)
        {
            this.source = source;
            buffers = OALW.GenBuffers(bufferCount);
            availableBuffers.AddRange(buffers);
            dequeueAudioCTS = new CancellationTokenSource();

            // Store the task so we can wait for it in Dispose
            dequeueTask = Task.Run(() => DequeueAudioLoop(dequeueAudioCTS.Token));
        }

        public void QueueAudio(byte[] audio, ALFormat format, int frequency)
        {
            FreeProcessedBuffers();

            // We aren't playing back audio fast enough, better to skip the audio
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

        // Renamed and made synchronous to avoid async void anti-pattern
        private void DequeueAudioLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                FreeProcessedBuffers();
                Thread.Sleep(30); // Use Thread.Sleep instead of await Task.Delay
            }
        }

        private void FreeProcessedBuffers()
        {
            bool sourceHasStopped;

            lock (buffer_queue_lock)
            {
                var sourceState = OALW.GetSourceState(source);
                sourceHasStopped = sourceState == ALSourceState.Stopped;

                // Only process buffers once after stopping
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

            // Add safety counter to prevent infinite loop if OpenAL fails
            var buffer = OALW.SourceUnqueueBuffer(source);
            int safetyCounter = 0;
            const int maxIterations = 100; // Protection against infinite loop

            while (buffer != 0 && safetyCounter < maxIterations)
            {
                queuedBuffers.Remove(buffer);
                availableBuffers.Add(buffer);
                buffer = OALW.SourceUnqueueBuffer(source);
                safetyCounter++;
            }

            // Log if we hit the safety limit
            if (safetyCounter >= maxIterations)
            {
                Logger.client.Warning($"CircularAudioBuffer: Hit max iterations while dequeuing buffers");
            }
        }

        public void Dispose()
        {
            // Wait for the dequeue task to finish before releasing resources
            dequeueAudioCTS?.Cancel();

            try
            {
                // Wait max 1 second for the task to complete
                dequeueTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
                // Task was cancelled, this is expected
            }

            dequeueAudioCTS?.Dispose();

            // Clean up buffers before deleting them
            lock (buffer_queue_lock)
            {
                // Empty the OpenAL queue before deleting buffers
                try
                {
                    OALW.SourceStop(source);

                    // Dequeue all remaining buffers
                    while (queuedBuffers.Count > 0)
                    {
                        var buffer = OALW.SourceUnqueueBuffer(source);
                        if (buffer == 0) break;
                        queuedBuffers.Remove(buffer);
                    }
                }
                catch (Exception e)
                {
                    Logger.client.Warning($"Error cleaning up audio buffers: {e.Message}");
                }

                OALW.DeleteBuffers(buffers);
            }
        }
    }
}