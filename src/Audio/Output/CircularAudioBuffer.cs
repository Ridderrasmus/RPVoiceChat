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
        public event Action OnEmptyingQueue;

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
                
                // Check for errors after BufferData
                OALW.BufferData(currentBuffer, format, audio, frequency);
                var bufferError = AL.GetError();
                if (bufferError != ALError.NoError)
                {
                    Logger.client.Warning($"OpenAL error while setting buffer data: {bufferError}");
                    availableBuffers.Add(currentBuffer); // Return buffer to available pool
                    return;
                }
                
                // Check for errors after SourceQueueBuffer
                OALW.SourceQueueBuffer(source, currentBuffer);
                var queueError = AL.GetError();
                if (queueError != ALError.NoError)
                {
                    Logger.client.Warning($"OpenAL error while queuing buffer: {queueError}");
                    availableBuffers.Add(currentBuffer); // Return buffer to available pool
                    return;
                }
                
                queuedBuffers.Add(currentBuffer);
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

                queuedBuffers.Remove(buffer);
                availableBuffers.Add(buffer);
            }
        }

        public void Dispose()
        {
            lock (buffer_queue_lock)
            {
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