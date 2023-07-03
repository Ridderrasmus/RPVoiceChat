using NAudio.Dsp;
using NAudio.Wave;
using System;

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

        public static byte[] HandleAudioPeaking(byte[] audio)
        {
            return ClampAudio(audio, 10000);
        }
        public static byte[] ClampAudio(byte[] audioData, short maxAmplitude)
        {
            // Create a new byte array to hold the clamped audio data
            byte[] clampedData = new byte[audioData.Length];

            // Iterate over the audio data, two bytes at a time (since each sample is 16 bits = 2 bytes)
            for (int i = 0; i < audioData.Length; i += 2)
            {
                // Convert the two bytes to a 16-bit signed integer
                short sample = BitConverter.ToInt16(audioData, i);

                // Clamp the sample to the maximum amplitude
                if (sample > maxAmplitude)
                {
                    sample = maxAmplitude;
                }
                else if (sample < -maxAmplitude)
                {
                    sample = (short)-maxAmplitude;
                }

                // Convert the clamped sample back to two bytes
                byte[] sampleBytes = BitConverter.GetBytes(sample);

                // Copy the two bytes to the clamped data array
                Array.Copy(sampleBytes, 0, clampedData, i, 2);
            }

            // Return the clamped data
            return clampedData;
        }

        public static int CalculateAmplitude(byte[] buffer, int validBytes)
        {
            // Convert to 16-bit samples.
            short[] samples = new short[buffer.Length / 2];
            Buffer.BlockCopy(buffer, 0, samples, 0, buffer.Length);

            // Calculate RMS amplitude.
            int sum = 0;
            foreach (var sample in samples)
            {
                sum += sample * sample;
            }
            int rms = (int)Math.Sqrt(sum / samples.Length);

            return Math.Max(Math.Min(rms / 10, 100), 0);
        }

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

    
}