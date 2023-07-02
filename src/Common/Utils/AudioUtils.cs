using Concentus.Structs;
using Concentus.Enums;
using System;
using System.Runtime;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using ProtoBuf;
using System.Security.Cryptography.X509Certificates;
using Vintagestory.API.Util;
using OpenTK.Audio.OpenAL;

namespace rpvoicechat
{
    public class AudioUtils
    {
        public static readonly int sampleRate = 24000;
        public OpusEncoder encoder;
        public OpusDecoder decoder;
        private static readonly AudioUtils _instance = new AudioUtils();
        public static AudioUtils Instance { get { return _instance; } }

        static AudioUtils()
        {
            Instance.encoder = OpusEncoder.Create(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            Instance.decoder = OpusDecoder.Create(sampleRate, 1);
        }

        private AudioUtils()
        {
        }

        public static byte[] ApplyMuffling(byte[] audioData)
        {
            return ConvertShortsToBytes(ApplyLowPassFilter(ConvertBytesToShorts(audioData), 1000, sampleRate));
        }
        
        // Apply a low pass filter to audio samples
        public static short[] ApplyLowPassFilter(short[] audioSamples, float cutoffFrequency, float sampleRate)
        {
            float _alpha = cutoffFrequency / (cutoffFrequency + sampleRate);
            float _lastSample = 0;
            short[] outputSamples = new short[audioSamples.Length];

            for (int i = 0; i < audioSamples.Length; i++)
            {
                _lastSample = _alpha * audioSamples[i] + (1 - _alpha) * _lastSample;
                outputSamples[i] = (short)_lastSample;
            }

            return outputSamples;

        }

        // Calculate the amplitude of audio samples
        public static int CalculateAmplitude(byte[] buffer, int bytesRecorded)
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

        // Shockingly, convert bytes to shorts
        public static short[] ConvertBytesToShorts(byte[] audioData)
        {
            short[] output = new short[audioData.Length / 2];
            Buffer.BlockCopy(audioData, 0, output, 0, audioData.Length);
            return output;
        }

        // Shockingly, convert shorts to bytes
        public static byte[] ConvertShortsToBytes(short[] audioData)
        {
            byte[] output = new byte[Math.Min(audioData.Length * 2, 2048)];
            Buffer.BlockCopy(audioData, 0, output, 0, output.Length);
            return output;
        }

        // Encode audio samples from a byte array
        internal static byte[] EncodeAudio(byte[] buffer)
        {
            return EncodeAudio(ConvertBytesToShorts(buffer));
        }

        // Encode audio samples from a short array
        internal static byte[] EncodeAudio(short[] buffer)
        {
            // Define the maximum size for the encoded data
            int maxEncodedSize = 2048; // Arbitrary number, you may need to adjust.
            byte[] encodedBuffer = new byte[maxEncodedSize];

            // Encode the audio samples
            int encodedLength = Instance.encoder.Encode(buffer, 0, 2048, encodedBuffer, 0, encodedBuffer.Length);

            // Resize the output array to match the actual size of the encoded data
            byte[] output = new byte[encodedLength];
            Array.Copy(encodedBuffer, output, encodedLength);

            return output;
        }

        // Decode audio samples from a byte array
        internal static byte[] DecodeAudio(byte[] encodedBuffer)
        {
            // Get the length of the encoded data
            int encodedLength = encodedBuffer.Length;

            // Define the buffer for the decoded data
            int maxDecodedSize = 1920; // 480 samples per frame (48kHz / 100) * 2 bytes per sample * 2 for stereo.
            short[] decodedBuffer = new short[maxDecodedSize];

            // Decode the audio samples
            int decodedLength = Instance.decoder.Decode(encodedBuffer, 0, encodedLength, decodedBuffer, 0, decodedBuffer.Length, false);

            // Resize the output array to match the actual size of the decoded data
            byte[] output = new byte[decodedLength];
            Array.Copy(ConvertShortsToBytes(decodedBuffer), output, decodedLength);

            return output;
        }

        // Decode audio samples from a short array
        internal static byte[] DecodeAudio(short[] encodedBuffer)
        {
            return DecodeAudio(ConvertShortsToBytes(encodedBuffer));
        }
    }
    // Class for sending audio data
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PlayerAudioPacket
    {
        public byte[] audioData;
        public Vec3d audioPos;
        public string playerUid;
        public VoiceLevel voiceLevel;
        public AudioSource audioSource;
    }

    public class PlayerAudioSource
    {
        public Vec3d audioPos;
        public bool isMuffled;
        public int sourceNum;

        public PlayerAudioSource(Vec3d audioPos, bool isMuffled)
        {
            this.audioPos = audioPos;
            this.isMuffled = isMuffled;
            this.sourceNum = AL.GenSource();
        }
    }

    // Enum for voice levels
    public enum VoiceLevel
    {
        Whisper = 5,
        Normal = 15,
        Shout = 30
    }

    // Enum for audio source
    public enum AudioSource
    {
        Person,
        HandheldRadio,
        StationaryRadio
    }
}
