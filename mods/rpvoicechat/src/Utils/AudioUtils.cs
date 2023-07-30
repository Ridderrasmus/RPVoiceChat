using NAudio.Dsp;
using System;
using System.Collections.Concurrent;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

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

        public static int CalculateAmplitude(short[] buffer, int validBytes)
        {
            // Convert to 16-bit samples.
            Buffer.BlockCopy(buffer, 0, buffer, 0, buffer.Length);

            // Calculate RMS amplitude.
            int sum = 0;
            foreach (var sample in buffer)
            {
                sum += sample * sample;
            }
            int rms = (int)Math.Sqrt(sum / validBytes);

            return Math.Max(Math.Min(rms / 10, 100), 0);
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

        // Reverb is not implemented yet
        public static byte[] ApplyReverb(byte[] input, int delayMilliseconds, float decay)
        {

            // Convert the byte array to a float array
            float[] floatInput = new float[input.Length / 2];
            for (int i = 0; i < floatInput.Length; i++)
            {
                floatInput[i] = BitConverter.ToInt16(input, i * 2) / 32768f;
            }

            // Create the delay buffer
            float[] delayBuffer = new float[floatInput.Length * delayMilliseconds / 1000];
            int delayPosition = 0;

            // Apply the delay line
            for (int i = 0; i < floatInput.Length; i++)
            {
                float sourceSample = floatInput[i];
                float delaySample = delayBuffer[delayPosition];
                delayBuffer[delayPosition] = sourceSample + delaySample * decay;
                floatInput[i] += delaySample;
                delayPosition++;
                if (delayPosition >= delayBuffer.Length)
                {
                    delayPosition = 0;
                }
            }

            // Convert the float array back to a byte array
            byte[] output = new byte[floatInput.Length * 2];
            for (int i = 0; i < floatInput.Length; i++)
            {
                short sample = (short)(floatInput[i] * 32768f);
                BitConverter.GetBytes(sample).CopyTo(output, i * 2);
            }

            return output;
        }

        // Change the volume of the audio data based on the distance
        public static byte[] VolumeBasedOnDistance(byte[] audioData, double distance, int voiceLevel)
        {
            short[] audioSamples = new short[audioData.Length / 2];
            Buffer.BlockCopy(audioData, 0, audioSamples, 0, audioData.Length);

            // Calculate the volume factor based on the distance
            float volumeFactor = CalculateVolumeFactor((float)distance, (float)voiceLevel);

            // Adjust the volume of each sample
            for (int i = 0; i < audioSamples.Length; i++)
            {
                audioSamples[i] = (short)(audioSamples[i] * volumeFactor);
            }

            // Convert the audio samples back to a byte array
            Buffer.BlockCopy(audioSamples, 0, audioData, 0, audioData.Length);

            return audioData;
        }

        private static float CalculateVolumeFactor(float distance, float maxDistance)
        {
            // Ensure the distance is within the valid range
            distance = Math.Max(0, Math.Min(distance, maxDistance));

            // Calculate the volume factor
            float volumeFactor = 1 - (distance / maxDistance);

            return volumeFactor;
        }

        public static byte[] MakeStereoFromMono(byte[] monoData)
        {
            // Create a new byte array for the stereo data
            byte[] stereoData = new byte[monoData.Length * 2];

            // Convert the mono data to stereo data
            for (int i = 0; i < monoData.Length / 2; i++)
            {
                // Get the mono sample
                short monoSample = BitConverter.ToInt16(monoData, i * 2);

                // Store the mono sample in both the left and right channels
                BitConverter.GetBytes(monoSample).CopyTo(stereoData, i * 4);
                BitConverter.GetBytes(monoSample).CopyTo(stereoData, i * 4 + 2);
            }

            return stereoData;
        }

        public static byte[] MakeStereoFromMono(byte[] monoData, EntityPos listenerPos, Vec3d audioPos)
        {
            // Calculate the direction from position1 to position2
            float direction = CalculateDirectionInRadians(listenerPos, audioPos);

            // Convert direction to a value between -1 (left) and 1 (right)
            float pan = (float)Math.Sin(direction);

            // Create a new byte array for the stereo data
            byte[] stereoData = new byte[monoData.Length * 2];

            // Convert the mono data to stereo data
            for (int i = 0; i < monoData.Length / 2; i++)
            {
                // Get the mono sample
                short monoSample = BitConverter.ToInt16(monoData, i * 2);

                // Calculate the left and right samples
                short leftSample = (short)(monoSample * (1 + pan) / 2);
                short rightSample = (short)(monoSample * (1 - pan) / 2);

                // Store the left and right samples in the stereo data array
                BitConverter.GetBytes(leftSample).CopyTo(stereoData, i * 4);
                BitConverter.GetBytes(rightSample).CopyTo(stereoData, i * 4 + 2);
            }

            return stereoData;
        }

        public static float CalculateDirectionInRadians(EntityPos listenerPos, Vec3d audioPos)
        {
            // Calculate the difference between the two positions
            Vec3d difference = listenerPos.XYZ - audioPos;

            // Calculate the direction in radians
            float direction = (float)Math.Atan2(difference.Z, -difference.X);

            // Account for the listener yaw
            direction -= (listenerPos.Yaw + listenerPos.HeadYaw);

            // Ensure the direction is between -pi and pi
            while (direction < -Math.PI)
            {
                direction += (float)(Math.PI * 2);
            }

            return direction;
        }
    }


    // Because reverb is a little more complex than the other effects, it has its own class
    public class ReverbEffect
    {
        private float[] delayBuffer;
        private int delayPosition;
        private int ChunkSize = (int)(AudioUtils.sampleRate * 2 * 0.02f);

        public float Decay { get; set; } = 0.4f;
        public ConcurrentQueue<byte[]> ReverbQueue = new ConcurrentQueue<byte[]>();

        public ReverbEffect(int delayMilliseconds)
        {
            delayBuffer = new float[AudioUtils.sampleRate * delayMilliseconds / 1000];
        }

        public void SetNewDelayMilliseconds(int delayMilliseconds)
        {
            Array.Resize(ref delayBuffer, AudioUtils.sampleRate * delayMilliseconds / 1000);
        }

        public void ApplyReverb(byte[] input)
        {

            // Convert the byte array to a float array
            float[] floatInput = new float[input.Length / 2];
            for (int i = 0; i < floatInput.Length; i++)
            {
                floatInput[i] = BitConverter.ToInt16(input, i * 2) / 32768f;
            }

            // Apply the delay line
            for (int i = 0; i < floatInput.Length; i++)
            {
                float sourceSample = floatInput[i];
                float delaySample = delayBuffer[delayPosition];
                delayBuffer[delayPosition] = sourceSample + delaySample * Decay;
                floatInput[i] += delaySample;
                delayPosition++;
                if (delayPosition >= delayBuffer.Length)
                {
                    delayPosition = 0;
                }
            }

            // Convert the float array back to a byte array
            byte[] output = new byte[floatInput.Length * 2];
            for (int i = 0; i < floatInput.Length; i++)
            {
                short sample = (short)(floatInput[i] * 32768f);
                BitConverter.GetBytes(sample).CopyTo(output, i * 2);
            }


            // Add the reverb echoes to the reverbQueue
            ReverbQueue.Enqueue(output);
        }

        public void ProcessAudioStream(byte[] audioStream)
        {
            // Split the audio stream into chunks
            for (int i = 0; i < audioStream.Length; i += ChunkSize)
            {
                // Get the next chunk of audio data
                byte[] chunk = new byte[ChunkSize];
                Array.Copy(audioStream, i, chunk, 0, ChunkSize);

                // Apply the reverb effect to the chunk
                ApplyReverb(chunk);
            }
        }
    }
}