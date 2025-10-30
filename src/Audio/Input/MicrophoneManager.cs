using OpenTK.Audio.OpenAL;
using RPVoiceChat.Audio.Effects;
using RPVoiceChat.Config;
using RPVoiceChat.Util;
using System;
using System.Collections.Concurrent;
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
        public event Action TransmissionStateChanged;

        // TODO: split MicrophoneManager into 3 classes
        // Audio capture
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
        private double _maxVolume => ServerConfigManager.MaxVolumeLimit;
        private short maxSampleValue;
        private List<float> recentGainLimits = new List<float>();
        private ConcurrentQueue<float> recentGainLimitsQueue = new ConcurrentQueue<float>();

        // Application interface/audio management
        public double Amplitude { get; private set; }
        public bool IsDenoisingAvailable = false;
        public bool Transmitting = false;
        public bool AudioWizardActive = false;
        private const int deactivationWindow = 4;
        private int stepsSinceLastTransmission = deactivationWindow;
        private bool transmittingOnPreviousStep = false;
        private ICoreClientAPI capi;
        private VoiceLevel voiceLevel = VoiceLevel.Talking;
        private double inputThreshold;
        private double maxInputThreshold;
        private List<double> recentAmplitudes = new List<double>();

        // Megaphone management (separate from voice level)
        private int customTransmissionRange = 0; // 0 = use default from VoiceLevel
        private bool ignoreDistanceReduction = false;
        private float wallThicknessOverride = -1f;
        private bool isGlobalBroadcast = false;

        public MicrophoneManager(ICoreClientAPI capi)
        {
            audioCaptureThread = new Thread(CaptureAudio);
            audioCaptureCTS = new CancellationTokenSource();
            this.capi = capi;
            maxSampleValue = (short)(_maxVolume * short.MaxValue);
            maxInputThreshold = _maxVolume / 2;
            SetThreshold(ModConfig.ClientConfig.InputThreshold);
            SetGain(ModConfig.ClientConfig.InputGain);
            SetOutputFormat(ALFormat.Mono16);
            SetCodec(OpusCodec._Name);
            capture = CreateNewCapture(ModConfig.ClientConfig.InputDevice);
            denoiser = TryLoadDenoiser();
        }

        public void Launch()
        {
            audioCaptureThread.Start(audioCaptureCTS.Token);
            capture?.Start();
        }

        public double GetMaxInputThreshold()
        {
            return maxInputThreshold;
        }

        public double GetInputThreshold()
        {
            return inputThreshold;
        }

        public void SetThreshold(float threshold)
        {
            inputThreshold = threshold * maxInputThreshold;
        }

        public void SetGain(float newGain)
        {
            gain = newGain;
        }

        public void SetDenoisingSensitivity(float sensitivity)
        {
            denoiser?.SetBackgroundNoiseThreshold(sensitivity);
        }

        public void SetDenoisingStrength(float strength)
        {
            denoiser?.SetVoiceDenoisingStrength(strength);
        }

        public void SetTransmissionRange(int rangeBlocks)
        {
            customTransmissionRange = Math.Max(0, rangeBlocks);
        }

        public int GetTransmissionRange()
        {
            return customTransmissionRange;
        }

        public void ResetTransmissionRange()
        {
            customTransmissionRange = 0;
        }

        public List<float> GetRecentGainLimits()
        {
            var recentGainLimits = new List<float>();

            float gainLimit;
            while (recentGainLimitsQueue.Count > 0)
                if (recentGainLimitsQueue.TryDequeue(out gainLimit))
                    recentGainLimits.Add(gainLimit);

            return recentGainLimits;
        }

        public void SetIgnoreDistanceReduction(bool ignore)
        {
            ignoreDistanceReduction = ignore;
        }

        public bool GetIgnoreDistanceReduction()
        {
            return ignoreDistanceReduction;
        }

        public void SetWallThicknessOverride(float thickness)
        {
            wallThicknessOverride = thickness;
        }

        public float GetWallThicknessOverride()
        {
            return wallThicknessOverride;
        }

        public void ResetWallThicknessOverride()
        {
            wallThicknessOverride = -1f;
        }

        public void SetGlobalBroadcast(bool enabled)
        {
            isGlobalBroadcast = enabled;
        }

        public bool GetGlobalBroadcast()
        {
            return isGlobalBroadcast;
        }

        public void ResetGlobalBroadcast()
        {
            isGlobalBroadcast = false;
        }


        private void CaptureAudio(object cancellationToken)
        {
            CancellationToken ct = (CancellationToken)cancellationToken;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Use WaitHandle instead of Thread.Sleep for proper cancellation
                    // Use consistent timing to prevent CPU spikes with multiple players
                    int sleepMs = 100;
                    ct.WaitHandle.WaitOne(sleepMs);
                    if (ct.IsCancellationRequested) break;
                    
                    UpdateCaptureAudioSamples();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Logger.client.Warning($"Error in audio capture thread: {e.Message}");
                    // Continue running unless cancellation is requested
                    if (ct.IsCancellationRequested) break;
                }
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

            bool isMuted = ModConfig.ClientConfig.IsMuted;
            bool isSleeping = clientEntity.AnimManager.IsAnimationActive("sleep");
            bool canSkipProcessing = isMuted || isSleeping || !clientEntity.Alive;
            bool forceProcessing = AudioWizardActive;
            if (canSkipProcessing && !forceProcessing)
            {
                if (recentAmplitudes.Count == 0) return;
                recentAmplitudes.Clear();
                Amplitude = 0;
                Transmitting = false;
                TransmissionStateChanged?.Invoke();
                transmittingOnPreviousStep = Transmitting;
                stepsSinceLastTransmission = deactivationWindow;
                return;
            }

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
            recentGainLimitsQueue.Enqueue(maxSafeGain);
            if (recentGainLimitsQueue.Count > 10) recentGainLimitsQueue.TryDequeue(out _);
            float volumeAmplification = Math.Min(maxSafeGain, recentGainLimits.Average());

            // Denoise audio if applicable
            if (ModConfig.ClientConfig.Denoising && denoiser?.SupportsFormat(Frequency, OutputChannelCount, SampleSize * 8) == true)
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
            byte[] encodedAudio;
            if (isGlobalBroadcast && codec is OpusCodec opusCodec)
            {
                encodedAudio = opusCodec.EncodeForBroadcast(pcms);
            }
            else
            {
                encodedAudio = codec.Encode(pcms);
            }

            return new AudioData()
            {
                data = encodedAudio,
                frequency = Frequency,
                format = OutputFormat,
                amplitude = amplitude,
                voiceLevel = voiceLevel,
                codec = codec.Name,
                transmissionRangeBlocks = customTransmissionRange,
                ignoreDistanceReduction = this.ignoreDistanceReduction,
                wallThicknessOverride = this.wallThicknessOverride,
                isGlobalBroadcast = this.isGlobalBroadcast
            };
        }

        private void TransmitAudio(AudioData data)
        {
            // Smooth out amplitude changes
            recentAmplitudes.Add(data.amplitude);
            if (recentAmplitudes.Count > 3) recentAmplitudes.RemoveAt(0);
            Amplitude = Math.Max(data.amplitude, recentAmplitudes.Average());

            // Check if activation conditions are met
            bool isPTTKeyPressed = capi.Input.KeyboardKeyState[capi.Input.GetHotKeyByCode("voicechatPTT").CurrentMapping.KeyCode];
            bool isAboveInputThreshold = Amplitude >= inputThreshold;
            Transmitting = ModConfig.ClientConfig.PushToTalkEnabled ? isPTTKeyPressed : isAboveInputThreshold;

            // Apply deactivation timeout
            stepsSinceLastTransmission++;
            if (Transmitting) stepsSinceLastTransmission = 0;
            Transmitting = stepsSinceLastTransmission < deactivationWindow;

            // Trigger notifcation when start/stop transmitting
            if (Transmitting != transmittingOnPreviousStep)
                TransmissionStateChanged?.Invoke();
            transmittingOnPreviousStep = Transmitting;

            // Transmit
            if (Transmitting || AudioWizardActive) OnBufferRecorded?.Invoke(data);
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
                deviceName = newCapture?.CurrentDevice ?? deviceName;
                Logger.client.Debug($"Succesfully created an audio capture device with arguments: {deviceName}, {Frequency}, {format}, {BufferSize}");
            }
            catch (Exception e)
            {
                Logger.client.Error($"Could not create audio capture device {deviceName} in {format} format:\n{e}");
            }
            SetInputFormat(format);
            ModConfig.ClientConfig.InputDevice = deviceName ?? "Default";

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
                denoiser = new RNNoiseDenoiser(ModConfig.ClientConfig.InputThreshold, ModConfig.ClientConfig.DenoisingStrength);
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

            bool guessUsedChannels = ModConfig.ClientConfig.ChannelGuessing;
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

        public void SetVoiceLevel(VoiceLevel newLevel)
        {
            if (voiceLevel != newLevel)
            {
                voiceLevel = newLevel;
                VoiceLevelUpdated?.Invoke(voiceLevel);
            }
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