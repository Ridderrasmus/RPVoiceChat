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
        private List<int> availableBuffers = new List<int>();
        private List<int> queuedBuffers = new List<int>();
        private int[] buffers;
        private int source;

        public CircularAudioBuffer(int source, int bufferCount)
        {
            this.source = source;
            buffers = OALW.GenBuffers(bufferCount);
            availableBuffers.AddRange(buffers);

            dequeueAudioThread = new Thread(DequeueAudio);
            dequeueAudioThread.Start();
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

            OALW.ExecuteInContext(() =>
            {
                AL.BufferData(currentBuffer, format, audio, length, frequency);
                OALW.CheckError("Error buffer data");
                AL.SourceQueueBuffer(source, currentBuffer);
                OALW.CheckError("Error SourceQueueBuffer");
            });
            queuedBuffers.Add(currentBuffer);
        }

        private void DequeueAudio()
        {
            while (dequeueAudioThread.IsAlive)
            {
                TryDequeueBuffers();
                Thread.Sleep(30);
            }
        }

        public void TryDequeueBuffers()
        {
            if (queuedBuffers.Count == 0) return;

            OALW.ExecuteInContext(() =>
            {
                var buffer = AL.SourceUnqueueBuffer(source);
                OALW.CheckError("Error SourceUnqueueBuffer", ALError.InvalidValue);
                while (buffer != 0)
                {
                    queuedBuffers.Remove(buffer);
                    availableBuffers.Add(buffer);
                    buffer = AL.SourceUnqueueBuffer(source);
                    OALW.CheckError("Error SourceUnqueueBuffer", ALError.InvalidValue);
                }
            });
        }

        public void Dispose()
        {
            dequeueAudioThread?.Abort();
            OALW.DeleteBuffers(buffers);
        }
    }
}
