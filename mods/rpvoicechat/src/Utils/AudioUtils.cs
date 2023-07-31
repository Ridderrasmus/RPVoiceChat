using NAudio.Dsp;
using System;
using OpenTK.Audio;
using System.Collections.Concurrent;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace rpvoicechat
{
    public class AudioUtils
    {
        private static readonly AudioUtils _instance = new AudioUtils();
        public static AudioUtils Instance { get { return _instance; } }

        public static int sampleRate = 48000;

        static AudioUtils()
        {
        }

        private AudioUtils()
        {
        }

        // Apply a low pass filter to the audio data
        public static byte[] ApplyMuffling(byte[] audio)
        {
            return ApplyLowPassFilter(audio, sampleRate, 1000);
        }


        public static byte[] ApplyLowPassFilter(byte[] audioData, int sampleRate, float cutoffFrequency)
        {
            // Convert byte array to short array
            short[] shortArray = new short[audioData.Length / 2];
            Buffer.BlockCopy(audioData, 0, shortArray, 0, audioData.Length);

            // Convert short array to float array
            float[] floatArray = Array.ConvertAll(shortArray, s => s / 32768f);

            // Create a low pass filter
            var filter = BiQuadFilter.LowPassFilter(sampleRate, cutoffFrequency, 1);

            // Apply the filter
            for (int i = 0; i < floatArray.Length; i++)
            {
                floatArray[i] = filter.Transform(floatArray[i]);
            }

            // Convert float array back to short array
            shortArray = Array.ConvertAll(floatArray, f => (short)(f * 32767));

            // Convert short array back to byte array
            byte[] filteredAudioData = new byte[shortArray.Length * 2];
            Buffer.BlockCopy(shortArray, 0, filteredAudioData, 0, filteredAudioData.Length);

            return filteredAudioData;
        }
    }

    public class CircularAudioBuffer : IDisposable
    {
        private List<int> availableBuffers = new List<int>();
        private List<int> queuedBuffers = new List<int>();
        private int[] buffers;
        private int source;
        private ICoreClientAPI capi;
        public CircularAudioBuffer(int source, int bufferCount, ICoreClientAPI capi)
        {
            this.source = source;
            buffers = AL.GenBuffers(bufferCount);
            availableBuffers.AddRange(buffers);
            this.capi = capi;

        }

        public void QueueAudio(byte[] audio, int length, ALFormat format, int frequency)
        {
            // we arent playing back audio fast enough, better to skip the audio
            if (availableBuffers.Count == 0)
            {
                capi.ShowChatMessage("Had to skip buffer");
                return;
            }

            var currentBuffer = availableBuffers.PopOne();

            AL.BufferData(currentBuffer, format, audio, length, frequency);
            AL.SourceQueueBuffer(source, currentBuffer);
            queuedBuffers.Add(currentBuffer);
        }

        public void TryDequeBuffers()
        {
            // nothing to do
            if (queuedBuffers.Count == 0)
                return;

            var buffer = AL.SourceUnqueueBuffer(source);
            while (buffer != 0)
            {
                queuedBuffers.Remove(buffer);
                availableBuffers.Add(buffer);
                buffer = AL.SourceUnqueueBuffer(source);
            }
        }

        public void Dispose()
        {
            AL.DeleteBuffers(buffers);
        }
    }

    // Because reverb is a little more complex than the other effects, it has its own class
    public class ReverbEffect
    {
        private int effect;
        private int slot;
        private EffectsExtension efx;
        public ReverbEffect(EffectsExtension efx, int source)
        {
            this.efx = efx;
            effect = efx.GenEffect();
            slot = efx.GenAuxiliaryEffectSlot();

            efx.BindEffect(effect, EfxEffectType.Reverb);
            efx.Effect(effect, EfxEffectf.ReverbDecayTime, 3.0f);
            efx.Effect(effect, EfxEffectf.ReverbDecayHFRatio, 0.91f);
            efx.Effect(effect, EfxEffectf.ReverbDensity, 0.7f);
            efx.Effect(effect, EfxEffectf.ReverbDiffusion, 0.9f);
            efx.Effect(effect, EfxEffectf.ReverbRoomRolloffFactor, 3.1f);
            efx.Effect(effect, EfxEffectf.ReverbReflectionsGain, 0.723f);
            efx.Effect(effect, EfxEffectf.ReverbReflectionsDelay, 0.03f);
            efx.Effect(effect, EfxEffectf.ReverbGain, 0.23f);

            efx.AuxiliaryEffectSlot(slot, EfxAuxiliaryi.EffectslotEffect, effect);
            efx.BindSourceToAuxiliarySlot(source, slot, 0, 0);
        }
    }
}