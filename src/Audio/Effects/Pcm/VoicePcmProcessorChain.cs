using System;
using System.Runtime.InteropServices;
using OpenTK.Audio.OpenAL;
using RPVoiceChat.Config;

namespace RPVoiceChat.Audio.Effects
{
    internal sealed class VoicePcmProcessorChain
    {
        private readonly IPcmVoiceProcessor drunk;
        private readonly IPcmVoiceProcessor temporal;
        private string session;
        private long expectedSample, lastPacketAt;
        private int rate, channels;

        public VoicePcmProcessorChain(string playerId)
        {
            uint seed = 2166136261;
            foreach (char character in playerId ?? "voice") seed = (seed ^ character) * 16777619;
            drunk = new DrunkPcmProcessor(seed);
            temporal = new TemporalGlitchProcessor(seed ^ 0x9e3779b9);
        }

        public void Process(AudioData audio, VoiceEffectMode drunkMode, VoiceEffectMode temporalMode)
        {
            int channelCount = audio.format == ALFormat.Mono16 ? 1 : audio.format == ALFormat.Stereo16 ? 2 : 0;
            if (channelCount == 0 || audio.frequency < 8000 || audio.frequency > 48000 || audio.data.Length % (2 * channelCount) != 0)
            { Reset(); return; }
            long now = Environment.TickCount64;
            bool timed = audio.sampleCount > 0 && !string.IsNullOrEmpty(audio.captureSession);
            if (rate != audio.frequency || channels != channelCount || now - lastPacketAt > 150
                || (timed && (session != audio.captureSession || expectedSample != audio.captureSampleTime))) Reset();
            rate = audio.frequency; channels = channelCount;
            session = audio.captureSession; expectedSample = audio.captureSampleTime + audio.sampleCount; lastPacketAt = now;
            float drunkAmount = VoiceEffectStrength.Sanitize(audio.drunkStrength) * Scale(drunkMode);
            float temporalAmount = VoiceEffectStrength.Sanitize(audio.temporalStrength) * Scale(temporalMode);
            Span<short> samples = MemoryMarshal.Cast<byte, short>(audio.data.AsSpan());
            // Share the wet budget when both states apply, keeping the original voice prominent.
            drunk.Process(samples, rate, channels, drunkAmount, 1 - temporalAmount * 0.5f);
            temporal.Process(samples, rate, channels, temporalAmount, 1 - drunkAmount * 0.5f);
        }

        private static float Scale(VoiceEffectMode mode) => mode == VoiceEffectMode.Full ? 1 : mode == VoiceEffectMode.Reduced ? 0.45f : 0;
        public void Reset() { drunk.Reset(); temporal.Reset(); session = null; expectedSample = 0; lastPacketAt = 0; }
    }
}
