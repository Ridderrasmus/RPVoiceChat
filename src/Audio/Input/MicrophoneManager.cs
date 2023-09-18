using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Vintagestory.API.Client;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using System.Collections.Generic;
using RPVoiceChat.Utils;

namespace RPVoiceChat.Audio
{
    public class MicrophoneManager : IDisposable
    {
        public static int Frequency = 48000;
        public ALFormat InputFormat { get; private set; }
        private ALFormat OutputFormat;
        private int BufferSize = (int)(Frequency * 0.5);
        private float gain;
        private int InputChannelCount;
        private int OutputChannelCount;
        private const byte SampleToByte = 2;
        private double MaxInputThreshold;
        readonly ICoreClientAPI capi;

        private IAudioCapture capture;
        private IAudioCodec codec;
        private RPVoiceChatConfig config;
        private ConcurrentQueue<AudioData> audioDataQueue = new ConcurrentQueue<AudioData>();
        private Thread audioProcessingThread;
        private CancellationTokenSource audioProcessingCTS;

        private VoiceLevel voiceLevel = VoiceLevel.Talking;
        public bool canSwitchDevice = true;
        public bool Transmitting = false;
        public bool TransmittingOnPreviousStep = false;

        private long gameTickId = 0;

        private bool isRecording = false;
        private double inputThreshold;

        public double Amplitude { get; set; }
        public double AmplitudeAverage { get; set; }

        public event Action<AudioData> OnBufferRecorded;
        public event Action<VoiceLevel> VoiceLevelUpdated;
        public event Action ClientStartTalking;
        public event Action ClientStopTalking;

        private List<double> recentAmplitudes = new List<double>();

        public MicrophoneManager(ICoreClientAPI capi)
        {
            audioProcessingThread = new Thread(ProcessAudio);
            audioProcessingCTS = new CancellationTokenSource();
            this.capi = capi;
            config = ModConfig.Config;
            MaxInputThreshold = config.MaxInputThreshold;
            SetThreshold(config.InputThreshold);
            SetGain(config.InputGain);

            capture = CreateNewCapture(config.CurrentInputDevice);
            codec = CreateNewCodec(ALFormat.Mono16);
        }

        public void Launch()
        {
            audioProcessingThread.Start(audioProcessingCTS.Token);
            gameTickId = capi.Event.RegisterGameTickListener(UpdateCaptureAudioSamples, 100);
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

        public void Dispose()
        {
            capi.Event.UnregisterGameTickListener(gameTickId);
            gameTickId = 0;
            audioProcessingCTS?.Cancel();
            audioProcessingCTS?.Dispose();
            capture?.Stop();
            capture?.Dispose();
        }

        public void SetThreshold(int threshold)
        {
            inputThreshold = (threshold / 100.0) * MaxInputThreshold;
        }

        public void SetGain(int newGain)
        {
            gain = newGain / 100f;
        }

        public void UpdateCaptureAudioSamples(float deltaTime)
        {
            bool isMuted = config.IsMuted;
            var clientEntity = capi.World.Player?.Entity;

            if (isMuted || clientEntity == null || capture == null || !clientEntity.Alive || clientEntity.AnimManager.IsAnimationActive("sleep"))
                return;

            int samplesAvailable = capture.AvailableSamples;
            int frameSize = codec.GetFrameSize();
            int samplesToRead = samplesAvailable - samplesAvailable % frameSize;

            int bufferLength = samplesToRead * SampleToByte * InputChannelCount;
            if (samplesToRead <= 0) return;

            var sampleBuffer = new byte[bufferLength];
            capture.ReadSamples(sampleBuffer, samplesToRead);

            AudioData data = PreprocessRawAudio(sampleBuffer);
            audioDataQueue.Enqueue(data);
        }

        /// <summary>
        /// Converts audio to Mono16, applies gain and calculates amplitude
        /// </summary>
        private AudioData PreprocessRawAudio(byte[] rawSamples)
        {
            var rawSampleSize = SampleToByte * InputChannelCount;
            var pcmCount = rawSamples.Length / rawSampleSize;
            short[] pcms = new short[pcmCount];

            double sampleSquareSum = 0;

            for (var rawSampleIndex = 0; rawSampleIndex < rawSamples.Length; rawSampleIndex += rawSampleSize)
            {
                double pcm = 0;

                int[] usedChannels = DetectAudioChannels(rawSamples);
                for (var channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
                {
                    if (!usedChannels.Contains(channelIndex)) continue;
                    var sampleIndex = rawSampleIndex + channelIndex * SampleToByte;
                    var sample = BitConverter.ToInt16(rawSamples, sampleIndex);
                    pcm += sample;
                }
                pcm = pcm / usedChannels.Length;
                pcm = pcm * gain;

                var pcmIndex = rawSampleIndex / rawSampleSize;
                pcms[pcmIndex] = (short)pcm;

                sampleSquareSum += Math.Pow(pcm / short.MaxValue, 2);
            }

            var amplitude = Math.Sqrt(sampleSquareSum / pcmCount);

            byte[] opusEncodedAudio = codec.Encode(pcms);

            return new AudioData()
            {
                data = opusEncodedAudio,
                frequency = Frequency,
                format = OutputFormat,
                amplitude = amplitude,
                voiceLevel = voiceLevel,
            };
        }

        private void ProcessAudio(object cancellationToken)
        {
            CancellationToken ct = (CancellationToken)cancellationToken;
            while (audioProcessingThread.IsAlive && !ct.IsCancellationRequested)
            {
                if (!audioDataQueue.TryDequeue(out var data))
                {
                    Thread.Sleep(30);
                    continue;
                }

                Amplitude = data.amplitude;
                recentAmplitudes.Add(Amplitude);

                if (recentAmplitudes.Count > 3)
                {
                    recentAmplitudes.RemoveAt(0);

                    AmplitudeAverage = recentAmplitudes.Average();
                }

                // Handle Push to Talk
                if (config.PushToTalkEnabled)
                {
                    Transmitting = capi.Input.KeyboardKeyState[capi.Input.GetHotKeyByCode("voicechatPTT").CurrentMapping.KeyCode];
                }
                else
                {
                    Transmitting = Amplitude >= inputThreshold || AmplitudeAverage >= inputThreshold;
                }

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
        }

        // Returns the success of the method
        public bool ToggleRecording()
        {
            return (ToggleRecording(!isRecording) == !isRecording);
        }

        // Returns the recording status
        public bool ToggleRecording(bool mode)
        {
            if (!canSwitchDevice) return isRecording;
            canSwitchDevice = false;
            if (isRecording == mode) return mode;

            if (mode)
            {
                capture.Start();
            }
            else
            {
                capture.Stop();
            }

            isRecording = mode;

            canSwitchDevice = true;
            return isRecording;
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
            catch(Exception e)
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

            for (var rawSampleIndex = 0; rawSampleIndex < rawSampleSize*depth; rawSampleIndex += rawSampleSize)
            {
                for (var channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
                {
                    var sampleIndex = rawSampleIndex + channelIndex * monoSampleSize;
                    int sample = BitConverter.ToInt16(rawSamples, sampleIndex);
                    sampleSums[channelIndex] += Math.Abs(sample);
                }
            }

            for (var channelIndex = 0; channelIndex < InputChannelCount; channelIndex++)
            {
                var averageSampleValue = sampleSums[channelIndex] / depth;
                if (averageSampleValue > 5) usedChannels.Add(channelIndex);
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
    }
}
