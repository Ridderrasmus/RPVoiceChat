﻿using OpenTK.Audio.OpenAL;
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

            OALW.BufferData(currentBuffer, format, audio, length, frequency);
            OALW.SourceQueueBuffer(source, currentBuffer);
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

            OALW.GetSource(source, ALGetSourcei.BuffersProcessed, out var buffersProcessed);
            if (buffersProcessed == 0) return;

            var processedBuffers = queuedBuffers.GetRange(0, buffersProcessed);
            OALW.SourceUnqueueBuffers(source, buffersProcessed, processedBuffers.ToArray());
            queuedBuffers.RemoveRange(0, buffersProcessed);
            availableBuffers.AddRange(processedBuffers);
        }

        public void Dispose()
        {
            dequeueAudioThread?.Abort();
            OALW.DeleteBuffers(buffers);
        }
    }
}