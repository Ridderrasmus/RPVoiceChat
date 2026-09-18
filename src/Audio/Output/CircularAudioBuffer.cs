using OpenTK.Audio.OpenAL;
using RPVoiceChat.Util;
using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Util;

namespace RPVoiceChat.Audio
{
    public class CircularAudioBuffer : IDisposable
    {
        public event Action OnEmptyingQueue;

        private List<int> availableBuffers = new List<int>();
        private List<int> queuedBuffers = new List<int>();
        private int[] buffers;
        private readonly Dictionary<int, double> durations = new();
        private double queuedMilliseconds;
        private bool disposed;
        private int source;
        private ALSourceState previousSourceState = ALSourceState.Initial;
        private object buffer_queue_lock = new object();

        public CircularAudioBuffer(int source, int bufferCount)
        {
            this.source = source;
            buffers = OALW.GenBuffers(bufferCount);
            availableBuffers.AddRange(buffers);
        }

        public void QueueAudio(byte[] audio, ALFormat format, int frequency)
        {
            if (!TryQueueAudio(audio, format, frequency))
            {
                // Voice path: drop rather than block the dequeue loop.
                Logger.client.Debug("CircularAudioBuffer had to skip queuing audio");
            }
        }

        /// <summary>
        /// Queue PCM into OpenAL. Returns false when all buffers are in use (caller should wait).
        /// </summary>
        public bool TryQueueAudio(byte[] audio, ALFormat format, int frequency, double maxBufferedMilliseconds = double.PositiveInfinity)
        {
            if (disposed) return false;
            FreeProcessedBuffers();

            if (availableBuffers.Count == 0)
            {
                return false;
            }

            lock (buffer_queue_lock)
            {
                double duration = audio.Length * 1000d / (frequency * AudioUtils.ChannelsPerFormat(format) * 2);
                if (disposed || availableBuffers.Count == 0 || (queuedBuffers.Count > 0 && queuedMilliseconds + duration > maxBufferedMilliseconds))
                {
                    VoiceDiagnostics.Count("hardware-backpressure-poll");
                    return false;
                }

                int currentBuffer = availableBuffers[0];
                availableBuffers.RemoveAt(0);
                OALW.ClearError();

                OALW.BufferData(currentBuffer, format, audio, frequency);
                var bufferError = AL.GetError();
                if (bufferError != ALError.NoError)
                {
                    Logger.client.Warning($"OpenAL error while setting buffer data: {bufferError}");
                    availableBuffers.Add(currentBuffer);
                    return false;
                }

                // The source may have drained while BufferData uploaded the new PCM.
                // Reclaim that finished audio before appending and restarting.
                if (OALW.GetSourceState(source) == ALSourceState.Stopped) TryDequeueBuffers();
                OALW.SourceQueueBuffer(source, currentBuffer);
                var queueError = AL.GetError();
                if (queueError != ALError.NoError)
                {
                    Logger.client.Warning($"OpenAL error while queuing buffer: {queueError}");
                    availableBuffers.Add(currentBuffer);
                    return false;
                }

                queuedBuffers.Add(currentBuffer);
                durations[currentBuffer] = duration;
                queuedMilliseconds += duration;
                VoiceDiagnostics.Count("hardware-queued");
                VoiceDiagnostics.Peak("hardware-queue-ms", queuedMilliseconds);
                return true;
            }
        }


        private void FreeProcessedBuffers()
        {
            bool sourceHasStopped;

            lock (buffer_queue_lock)
            {
                var sourceState = OALW.GetSourceState(source);
                sourceHasStopped = sourceState == ALSourceState.Stopped;

                // Playback can start AND finish between polls. A repeated Stopped
                // observation does not mean that the processed buffers are unchanged.
                bool notifyStopped = sourceHasStopped && sourceState != previousSourceState;
                previousSourceState = sourceState;
                TryDequeueBuffers();
                sourceHasStopped = notifyStopped;
            }

            if (sourceHasStopped) OnEmptyingQueue?.Invoke();
        }

        private void TryDequeueBuffers()
        {
            if (queuedBuffers.Count == 0) return;

            OALW.ClearError();
            OALW.GetSource(source, ALGetSourcei.BuffersProcessed, out var buffersProcessed);

            if (buffersProcessed == 0) return;

            // Only dequeue the exact number of processed buffers
            for (int i = 0; i < buffersProcessed && queuedBuffers.Count > 0; i++)
            {
                var buffer = OALW.SourceUnqueueBuffer(source);
                if (buffer == 0) break; // No more buffers to dequeue

                // Check for OpenAL errors after dequeuing
                var error = AL.GetError();
                if (error != ALError.NoError)
                {
                    Logger.client.Warning($"OpenAL error while dequeuing buffer: {error}");
                    break;
                }

                if (!queuedBuffers.Remove(buffer)) break;
                if (durations.Remove(buffer, out double duration)) queuedMilliseconds -= duration;
                availableBuffers.Add(buffer);
                VoiceDiagnostics.Count("hardware-reclaimed");
            }
        }

        public void Reset()
        {
            lock (buffer_queue_lock)
            {
                if (disposed) return;
                OALW.SourceStop(source);
                while (queuedBuffers.Count > 0)
                {
                    int id = OALW.SourceUnqueueBuffer(source);
                    if (id == 0 || !queuedBuffers.Remove(id)) break;
                    availableBuffers.Add(id);
                }
                durations.Clear();
                queuedMilliseconds = 0;
                previousSourceState = ALSourceState.Initial;
            }
        }

        public void Dispose()
        {
            lock (buffer_queue_lock)
            {
                if (disposed) return;
                Reset();
                disposed = true;
                OALW.DeleteBuffers(buffers);
            }
        }
    }
}
