using System;
using System.Runtime.InteropServices;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Audio.Effects
{
    public class RNNoiseDenoiser : IDenoiser
    {
        #region Utility class
        private class PointerSource : IDisposable
        {
            private GCHandle handle;
            public IntPtr ptr;

            public PointerSource(Array buffer)
            {
                handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                ptr = handle.AddrOfPinnedObject();
            }

            public void Dispose()
            {
                handle.Free();
            }
        }
        #endregion

        private const int FRAME_SIZE = RNNoise.FRAME_SIZE;
        private IntPtr handle = IntPtr.Zero;
        private float sensitivity;
        private float strength;

        public RNNoiseDenoiser(float backgroundNoiseThreshold = 0.3f, float voiceDenoisingStrength = 0.6f)
        {
            handle = RNNoise.Create(IntPtr.Zero);
            SetBackgroundNoiseThreshold(backgroundNoiseThreshold);
            SetVoiceDenoisingStrength(voiceDenoisingStrength);
        }

        public void Denoise(ref short[] _pcms)
        {
            float[] rawAudio = Array.ConvertAll(_pcms, e => (float)e);
            using (var pcmBuffer = new PointerSource(rawAudio))
            {
                for (var offset = 0; offset < rawAudio.Length; offset += FRAME_SIZE)
                {
                    var remainingDataLength = rawAudio.Length - offset;
                    var denoisedAudio = new float[FRAME_SIZE];
                    using (var denoisedBuffer = new PointerSource(denoisedAudio))
                    {
                        var inPtr = pcmBuffer.ptr + offset * sizeof(float);
                        var outPtr = denoisedBuffer.ptr;

                        // If last frame is too small right pad it with zeros
                        if (remainingDataLength < FRAME_SIZE)
                        {
                            Array.Copy(rawAudio, offset, denoisedAudio, 0, remainingDataLength);
                            inPtr = outPtr;
                        }

                        float VAD = RNNoise.DenoiseFrame(handle, outPtr, inPtr);
                        bool isVoice = VAD > sensitivity;
                        if (isVoice)
                            for (var i = 0; i < denoisedAudio.Length; i++)
                                denoisedAudio[i] = denoisedAudio[i] * strength + rawAudio[offset + i] * (1 - strength);

                        denoisedAudio.CopyTo(rawAudio, offset);
                    }
                }
            }

            _pcms = Array.ConvertAll(rawAudio, e => (short)e);
        }

        public void SetBackgroundNoiseThreshold(float value)
        {
            sensitivity = GameMath.Clamp(value, 0, 1);
        }

        public void SetVoiceDenoisingStrength(float value)
        {
            strength = GameMath.Clamp(value, 0, 1);
        }

        public bool SupportsFormat(int frequency, int channels, int bits)
        {
            return frequency == 48000 && channels == 1 && bits == 16;
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            RNNoise.Destroy(handle);
        }
    }
}
