using Concentus.Structs;
using Concentus.Enums;
using ProtoBuf;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace rpvoicechat
{
    public class AudioUtils
    {
        public static readonly int sampleRate = 48000;
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

        // Method to make positional audio
        public static short[] MakePositionalAudio(EntityPos listenerEntityPos, Vec3d soundPos, byte[] audioData, VoiceLevel voiceLevel)
        {
            // Get listeners position
            Vec3d listenerPos = listenerEntityPos.XYZ;
            float listenerFacing = listenerEntityPos.Yaw;

            // Get the relative position from the audio source to the listener
            Vec3d relativePos = soundPos - listenerPos;

            // Calculate the dot product of the relative position and the listeners facing
            double dotProduct = relativePos.X * Math.Cos(listenerFacing) + relativePos.Z * Math.Sin(listenerFacing);

            // Calculate the panning value based on dot product
            double panning = GameMath.Clamp(dotProduct, -1.0, 1.0);

            // Calculate distance between source and listener
            double distance = relativePos.Length();

            // Define max distance based on voiceLevel
            int voiceLevelInt = (int)voiceLevel;
            double maxDistance = Convert.ToDouble(voiceLevelInt);

            // Calculate the volume scaling factor based on distance
            double volumeScaling = Math.Max(0, 1 - distance / maxDistance);

            // Adjust the left and right channels based on panning
            short[] audioSamples = AudioUtils.ConvertBytesToShorts(audioData);
            short[] stereoSamples = new short[audioSamples.Length * 2];
            for (int i = 0; i < audioSamples.Length; i++)
            {
                short sample = audioSamples[i];
                stereoSamples[i * 2] = (short)(sample * (1 - panning) * volumeScaling);
                stereoSamples[i * 2 + 1] = (short)(sample * (1 + panning) * volumeScaling);
            }


            return stereoSamples;
        }

        public static short[] ApplyMuffling(short[] audioData, IClientWorldAccessor world, Vec3d soundPos)
        {
            Vec3d listenerPos = world.Player.Entity.Pos.XYZ;

            // Raycast between the source and listener and return first entity and block that is hit
            BlockSelection blockSel = new BlockSelection();
            EntitySelection entitySel = new EntitySelection();
            world.RayTraceForSelection(new Vec3d(soundPos.X, soundPos.Y + 0.5, soundPos.Z), new Vec3d(listenerPos.X, listenerPos.Y + 0.5, listenerPos.Z), ref blockSel, ref entitySel);

            // If either a block or entity is hit muffle the audio
            if (blockSel != null || entitySel != null)
            {
                // Muffle the audio
                audioData = ApplyLowPassFilter(audioData, 1000, sampleRate);
            }

            return audioData;
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
        public static float CalculateAmplitude(byte[] buffer, int bytesRecorded)
        {
            double sum = 0;
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                // Convert bytes to 16-bit signed integer
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                sum += Math.Abs(sample); // Add the absolute value of amplitude
            }

            // Calculate average amplitude
            double avg = sum / (bytesRecorded / 2);

            // Normalize to [0,1]
            return (float)(avg / short.MaxValue);
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
            byte[] output = new byte[audioData.Length * 2];
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
            //return ConvertShortsToBytes(buffer);

            // Define the maximum size for the encoded data
            int maxEncodedSize = 4000; // Arbitrary number, you may need to adjust.
            byte[] encodedBuffer = new byte[maxEncodedSize];

            // Encode the audio samples
            int encodedLength = Instance.encoder.Encode(buffer, 0, buffer.Length, encodedBuffer, 0, encodedBuffer.Length);

            // Resize the output array to match the actual size of the encoded data
            byte[] output = new byte[encodedLength];
            Array.Copy(encodedBuffer, output, encodedLength);

            return output;
        }

        // Decode audio samples from a byte array
        internal static byte[] DecodeAudio(byte[] encodedBuffer)
        {
            //return encodedBuffer;
            
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

    // Enum for voice levels
    public enum VoiceLevel
    {
        Whisper = 10,
        Normal = 20,
        Shout = 50
    }

    // Enum for audio source
    public enum AudioSource
    {
        Person,
        HandheldRadio,
        StationaryRadio
    }
}
