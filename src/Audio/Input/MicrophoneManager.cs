using OpenTK.Audio.OpenAL;
using RPVoiceChat.Audio.Effects;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Audio
{
    public class MicrophoneManager : IDisposable
    {
        public event Action<AudioData> OnBufferRecorded;
        public event Action<VoiceLevel> VoiceLevelUpdated;
        public event Action ClientStartTalking;
        public event Action ClientStopTalking;

        private ICoreClientAPI capi;
        private IAudioCapture capture;
        private IAudioCodec codec;
        private IDenoiser denoiser;
        private RPVoiceChatConfig config;
        private Thread audioCaptureThread;
        private CancellationTokenSource audioCaptureCTS;

        public static int Frequency = 48000;
        public ALFormat InputFormat { get; private set; }
        private ALFormat OutputFormat;
        private int BufferSize = (int)(Frequency * 0.5);
        private float gain;
        private int InputChannelCount;
        private int OutputChannelCount;
        private const byte SampleToByte = 2;

        public double Amplitude { get; private set; }
        public double AmplitudeAverage { get; private set; }
        public bool Transmitting = false;
        public bool TransmittingOnPreviousStep = false;
        public bool IsDenoisingAvailable = false;
        private VoiceLevel voiceLevel = VoiceLevel.Talking;
        private double inputThreshold;
        private double MaxInputThreshold;
        private List<double> recentAmplitudes = new List<double>();

        public MicrophoneManager(ICoreClientAPI capi)
        {
            audioCaptureThread = new Thread(CaptureAudio);
            audioCaptureCTS = new CancellationTokenSource();
            this.capi = capi;
            config = ModConfig.Config;
            MaxInputThreshold = config.MaxInputThreshold;
            SetThreshold(config.InputThreshold);
            SetGain(config.InputGain);

            capture = CreateNewCapture(config.CurrentInputDevice);
            codec = CreateNewCodec(ALFormat.Mono16);
            denoiser = TryLoadDenoiser();
        }

        public void Launch()
        {
            audioCaptureThread.Start(audioCaptureCTS.Token);
            capture?.Start();
        }

        public double GetMaxInputThreshold()
        {
            return MaxInputThreshold;
        }

        public void SetMaxInputThreshold(double maxInputThreshold)
        {
            int inputThreshold = (int)(GetInputThreshold() / MaxInputThreshold * 100);
            MaxInputThreshold = maxInputThreshold / 100;
            SetThreshold(inputThreshold);
        }

        public double GetInputThreshold()
        {
            return inputThreshold;
        }

        public void SetThreshold(int threshold)
        {
            inputThreshold = (threshold / 100.0) * MaxInputThreshold;
        }

        public void SetGain(int newGain)
        {
            gain = newGain / 100f;
        }

        public void SetDenoisingSensitivity(int sensitivity)
        {
            denoiser?.SetBackgroundNoiseThreshold(sensitivity / 100f);
        }

        public void SetDenoisingStrength(int strength)
        {
            denoiser?.SetVoiceDenoisingStrength(strength / 100f);
        }

        private void CaptureAudio(object cancellationToken)
        {
            CancellationToken ct = (CancellationToken)cancellationToken;
            while (audioCaptureThread.IsAlive && !ct.IsCancellationRequested)
            {
                Thread.Sleep(100);
                UpdateCaptureAudioSamples();
            }
        }

        /// <summary>
        /// Reads captured audio as frames, processes captured frames and transmits them
        /// </summary>
        private void UpdateCaptureAudioSamples()
        {
            var clientEntity = capi.World.Player?.Entity;
            if (clientEntity == null || capture == null) return;

            int samplesAvailable = capture.AvailableSamples;
            int frameSize = codec.FrameSize;
            int samplesToRead = samplesAvailable - samplesAvailable % frameSize;
            if (samplesToRead <= 0) return;
            int bufferLength = samplesToRead * SampleToByte * InputChannelCount;
            var sampleBuffer = new byte[bufferLength];
            capture.ReadSamples(sampleBuffer, samplesToRead);

            bool isMuted = config.IsMuted;
            bool isSleeping = clientEntity.AnimManager.IsAnimationActive("sleep");
            if (isMuted || isSleeping || !clientEntity.Alive) return;

            AudioData data = ProcessAudio(sampleBuffer);
            TransmitAudio(data);
        }

        /// <summary>
        /// Converts audio to Mono16, applies gain, applies denoising, calculates amplitude and applies encoding
        /// </summary>
        private AudioData ProcessAudio(byte[] rawSamples)
        {
            var rawSampleSize = SampleToByte * InputChannelCount;
            var pcmCount = rawSamples.Length / rawSampleSize;
            short[] pcms = new short[pcmCount];
            int[] usedChannels = DetectAudioChannels(rawSamples);

            for (var rawSampleIndex = 0; rawSampleIndex < rawSamples.Length; rawSampleIndex += rawSampleSize)
            {
                double pcm = 0;

                for (var channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
                {
                    if (!usedChannels.Contains(channelIndex)) continue;
                    var sampleIndex = rawSampleIndex + channelIndex * SampleToByte;
                    var sample = BitConverter.ToInt16(rawSamples, sampleIndex);
                    pcm += sample;
                }
                pcm = pcm / Math.Max(usedChannels.Length, 1);
                pcm = pcm * gain;

                var pcmIndex = rawSampleIndex / rawSampleSize;
                pcms[pcmIndex] = (short)GameMath.Clamp(pcm, short.MinValue, short.MaxValue);
            }

            if (config.IsDenoisingEnabled && denoiser != null && denoiser.SupportsFormat(Frequency, OutputChannelCount, SampleToByte * 8))
                denoiser.Denoise(ref pcms);

            double sampleSquareSum = 0;
            for (var i = 0; i < pcms.Length; i++)
                sampleSquareSum += Math.Pow((float)pcms[i] / short.MaxValue, 2);

            var amplitude = Math.Sqrt(sampleSquareSum / pcmCount);

            byte[] audio;
            bool shouldEncode = WorldConfig.GetBool("encode-audio");
            if (shouldEncode) audio = codec.Encode(pcms);
            else audio = AudioUtils.ShortsToBytes(pcms, 0, pcms.Length);
            string codecName = shouldEncode ? codec.Name : null;

            return new AudioData()
            {
                data = audio,
                frequency = Frequency,
                format = OutputFormat,
                amplitude = amplitude,
                voiceLevel = voiceLevel,
                codec = codecName,
            };
        }

        private void TransmitAudio(AudioData data)
        {
            Amplitude = data.amplitude;
            recentAmplitudes.Add(Amplitude);

            if (recentAmplitudes.Count > 3)
            {
                recentAmplitudes.RemoveAt(0);
                AmplitudeAverage = recentAmplitudes.Average();
            }

            // Handle Push to Talk
            bool isPTTKeyPressed = capi.Input.KeyboardKeyState[capi.Input.GetHotKeyByCode("voicechatPTT").CurrentMapping.KeyCode];
            bool isAboveInputThreshold = Amplitude >= inputThreshold || AmplitudeAverage >= inputThreshold;
            Transmitting = config.PushToTalkEnabled ? isPTTKeyPressed : isAboveInputThreshold;

            if (Transmitting)
            {
                if (!TransmittingOnPreviousStep) ClientStartTalking?.Invoke();
                OnBufferRecorded?.Invoke(data);
            }
            else if (TransmittingOnPreviousStep)
            {
                ClientStopTalking?.Invoke();
            }

            TransmittingOnPreviousStep = Transmitting;
        }

        private IAudioCapture CreateNewCapture(string deviceName, ALFormat? captureFormat = null)
        {
            ALFormat format = captureFormat ?? GetDefaultInputFormat();
            if (capture?.CurrentDevice == deviceName && capture?.SampleFormat == format)
                return capture;

            capture?.Stop();
            capture?.Dispose();

            IAudioCapture newCapture = null;
            try
            {
                newCapture = new OpenALAudioCapture(deviceName, Frequency, format, BufferSize);
                format = newCapture?.SampleFormat ?? format;
                Logger.client.Debug($"Succesfully created an audio capture device with arguments: {deviceName}, {Frequency}, {format}, {BufferSize}");
            }
            catch (Exception e)
            {
                Logger.client.Error($"Could not create audio capture device {deviceName} in {format} format:\n{e}");
            }
            SetInputFormat(format);
            config.CurrentInputDevice = deviceName ?? "Default";
            ModConfig.Save(capi);

            return newCapture;
        }

        private IAudioCodec CreateNewCodec(ALFormat outputFormat)
        {
            SetOutputFormat(outputFormat);

            var codec = new OpusCodec(Frequency, OutputChannelCount);

            return codec;
        }

        private IDenoiser TryLoadDenoiser()
        {
            IDenoiser denoiser = null;
            try
            {
                denoiser = new RNNoiseDenoiser(config.BackgroungNoiseThreshold, config.VoiceDenoisingStrength);
                IsDenoisingAvailable = true;
            }
            catch (DllNotFoundException)
            {
                Logger.client.Error("Can't find denoising library, denoising won't be available");
            }
            return denoiser;
        }

        private ALFormat GetDefaultInputFormat()
        {
            var format = ALFormat.Mono16;
            var supportedFormats = AL.Get(ALGetString.Extensions) ?? "";
            if (supportedFormats.Contains("AL_EXT_MCFORMATS"))
                format = ALFormat.MultiQuad16Ext;
            else
                Logger.client.Warning($"Multichannel audio capture is not available.");

            return format;
        }

        private void SetInputFormat(ALFormat format)
        {
            InputFormat = format;
            InputChannelCount = AudioUtils.ChannelsPerFormat(format);
        }

        private void SetOutputFormat(ALFormat format)
        {
            OutputFormat = format;
            OutputChannelCount = AudioUtils.ChannelsPerFormat(format);
        }

        /// <summary>
        /// Attempts to guess how many audio channels capture device uses
        /// </summary>
        /// <returns>
        /// Indices of channels containing audio data
        /// </returns>
        private int[] DetectAudioChannels(byte[] rawSamples)
        {
            const int depth = 10;
            List<int> usedChannels = new List<int>();
            var sampleSums = new int[InputChannelCount];

            var rawSampleSize = SampleToByte * InputChannelCount;
            int monoSampleSize = SampleToByte;

            for (var rawSampleIndex = 0; rawSampleIndex < rawSampleSize * depth; rawSampleIndex += rawSampleSize)
            {
                for (var channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
                {
                    var sampleIndex = rawSampleIndex + channelIndex * monoSampleSize;
                    int sample = BitConverter.ToInt16(rawSamples, sampleIndex);
                    sampleSums[channelIndex] += Math.Abs(sample);
                }
            }

            bool guessUsedChannels = ClientSettings.GetBool("channelGuessing", true);
            for (var channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
            {
                var averageSampleValue = sampleSums[channelIndex] / depth;
                if (averageSampleValue > 5 || !guessUsedChannels) usedChannels.Add(channelIndex);
            }

            return usedChannels.ToArray();
        }

        public string[] GetInputDeviceNames()
        {
            var devices = OpenALAudioCapture.GetAvailableDevices();
            devices.Insert(0, "Default");

            return devices.ToArray();
        }

        public VoiceLevel CycleVoiceLevel()
        {
            if (voiceLevel == VoiceLevel.Talking)
            {
                voiceLevel = VoiceLevel.Shouting;
            }
            else if (voiceLevel == VoiceLevel.Shouting)
            {
                voiceLevel = VoiceLevel.Whispering;
            }
            else if (voiceLevel == VoiceLevel.Whispering)
            {
                voiceLevel = VoiceLevel.Talking;
            }

            VoiceLevelUpdated?.Invoke(voiceLevel);
            return voiceLevel;
        }

        public VoiceLevel GetVoiceLevel()
        {
            return voiceLevel;
        }

        public void SetInputDevice(string deviceId)
        {
            capture = CreateNewCapture(deviceId);
            capture?.Start();
        }

        public void Dispose()
        {
            audioCaptureCTS?.Cancel();
            audioCaptureCTS?.Dispose();
            capture?.Stop();
            capture?.Dispose();
            denoiser?.Dispose();
        }
    }
}
