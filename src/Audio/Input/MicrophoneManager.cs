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

        // TODO: split MicrophoneManager into 3 classes
        // Audio cature
        public static int Frequency = 48000;
        private IAudioCapture capture;
        private Thread audioCaptureThread;
        private CancellationTokenSource audioCaptureCTS;
        private int BufferSize = (int)(Frequency * 0.5);
        private int InputChannelCount;
        private const byte SampleSize = sizeof(short);

        // Audio processing
        private IAudioCodec codec;
        private IDenoiser denoiser;
        private ALFormat OutputFormat;
        private int OutputChannelCount;
        private float gain;
        private const double _maxVolume = 0.7;
        private const short maxSampleValue = (short)(_maxVolume * short.MaxValue);
        private List<float> recentGainLimits = new List<float>();

        // Aplication interface/audio management
        public double Amplitude { get; private set; }
        public bool IsDenoisingAvailable = false;
        public bool Transmitting = false;
        private bool TransmittingOnPreviousStep = false;
        private ICoreClientAPI capi;
        private RPVoiceChatConfig config;
        private VoiceLevel voiceLevel = VoiceLevel.Talking;
        private double inputThreshold;
        private double MaxInputThreshold = _maxVolume / 2;
        private List<double> recentAmplitudes = new List<double>();

        public MicrophoneManager(ICoreClientAPI capi)
        {
            audioCaptureThread = new Thread(CaptureAudio);
            audioCaptureCTS = new CancellationTokenSource();
            this.capi = capi;
            config = ModConfig.Config;
            SetThreshold(config.InputThreshold);
            SetGain(ClientSettings.InputGain);
            SetOutputFormat(ALFormat.Mono16);
            SetCodec(OpusCodec._Name);
            capture = CreateNewCapture(config.CurrentInputDevice);
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

        public double GetInputThreshold()
        {
            return inputThreshold;
        }

        public void SetThreshold(int threshold)
        {
            inputThreshold = (threshold / 100.0) * MaxInputThreshold;
        }

        public void SetGain(float newGain)
        {
            gain = newGain;
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

            bool shouldEncode = WorldConfig.GetBool("encode-audio");
            var targetCodec = shouldEncode ? OpusCodec._Name : DummyCodec._Name;
            SetCodec(targetCodec);

            int samplesAvailable = capture.AvailableSamples;
            int frameSize = codec.FrameSize;
            int samplesToRead = samplesAvailable - samplesAvailable % frameSize;
            if (samplesToRead <= 0) return;
            int bufferLength = samplesToRead * SampleSize * InputChannelCount;
            var sampleBuffer = new byte[bufferLength];
            capture.ReadSamples(sampleBuffer, samplesToRead);

            bool isMuted = config.IsMuted;
            bool isSleeping = clientEntity.AnimManager.IsAnimationActive("sleep");
            if (isMuted || isSleeping || !clientEntity.Alive) return;

            AudioData data = ProcessAudio(sampleBuffer);
            TransmitAudio(data);
        }

        /// <summary>
        /// Converts audio to Mono16, applies denoising, applies gain, calculates amplitude and encodes the result
        /// </summary>
        private AudioData ProcessAudio(byte[] rawSamples)
        {
            var rawSampleSize = SampleSize * InputChannelCount;
            var pcmCount = rawSamples.Length / rawSampleSize;
            short[] pcms = new short[pcmCount];
            int[] usedChannels = DetectAudioChannels(rawSamples);
            double peakPcmValue = 1;

            // Convert audio to mono, find peaks
            for (var rawSampleIndex = 0; rawSampleIndex < rawSamples.Length; rawSampleIndex += rawSampleSize)
            {
                double pcm = 0;
                for (var channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
                {
                    if (!usedChannels.Contains(channelIndex)) continue;
                    var sampleIndex = rawSampleIndex + channelIndex * SampleSize;
                    var sample = BitConverter.ToInt16(rawSamples, sampleIndex);
                    pcm += sample;
                }
                pcm /= Math.Max(usedChannels.Length, 1);

                var abs = Math.Abs(pcm);
                if (abs > peakPcmValue) peakPcmValue = abs;

                var pcmIndex = rawSampleIndex / rawSampleSize;
                pcms[pcmIndex] = (short)pcm;
            }

            // Calculate volume amplification
            float maxSafeGain = Math.Min(gain, (float)(maxSampleValue / peakPcmValue));
            recentGainLimits.Add(maxSafeGain);
            if (recentGainLimits.Count > 10) recentGainLimits.RemoveAt(0);
            float volumeAmplification = Math.Min(maxSafeGain, recentGainLimits.Average());

            // Denoise audio if applicable
            if (config.IsDenoisingEnabled && denoiser != null && denoiser.SupportsFormat(Frequency, OutputChannelCount, SampleSize * 8))
                denoiser.Denoise(ref pcms);

            // Amplify volume and calculate amplitude
            double sampleSquareSum = 0;
            for (var i = 0; i < pcms.Length; i++)
            {
                pcms[i] = (short)GameMath.Clamp(pcms[i] * volumeAmplification, short.MinValue, short.MaxValue);
                sampleSquareSum += Math.Pow((float)pcms[i] / short.MaxValue, 2);
            }
            var amplitude = Math.Sqrt(sampleSquareSum / pcmCount);

            // Encode audio
            byte[] encodedAudio = codec.Encode(pcms);

            return new AudioData()
            {
                data = encodedAudio,
                frequency = Frequency,
                format = OutputFormat,
                amplitude = amplitude,
                voiceLevel = voiceLevel,
                codec = codec.Name,
            };
        }

        private void TransmitAudio(AudioData data)
        {
            recentAmplitudes.Add(data.amplitude);
            if (recentAmplitudes.Count > 3) recentAmplitudes.RemoveAt(0);
            Amplitude = Math.Max(data.amplitude, recentAmplitudes.Average());

            bool isPTTKeyPressed = capi.Input.KeyboardKeyState[capi.Input.GetHotKeyByCode("voicechatPTT").CurrentMapping.KeyCode];
            bool isAboveInputThreshold = Amplitude >= inputThreshold;
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

        private void SetCodec(string codecName)
        {
            if (codec?.Name == codecName && codec?.Channels == OutputChannelCount) return;

            codec = codecName switch
            {
                OpusCodec._Name => new OpusCodec(Frequency, OutputChannelCount),
                DummyCodec._Name => new DummyCodec(Frequency, OutputChannelCount),
                _ => throw new ArgumentException($"{codecName} is not a valid codec name")
            };
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

            var rawSampleSize = SampleSize * InputChannelCount;
            int monoSampleSize = SampleSize;

            for (var rawSampleIndex = 0; rawSampleIndex < rawSampleSize * depth; rawSampleIndex += rawSampleSize)
            {
                for (var channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
                {
                    var sampleIndex = rawSampleIndex + channelIndex * monoSampleSize;
                    int sample = BitConverter.ToInt16(rawSamples, sampleIndex);
                    sampleSums[channelIndex] += Math.Abs(sample);
                }
            }

            bool guessUsedChannels = ClientSettings.ChannelGuessing;
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
