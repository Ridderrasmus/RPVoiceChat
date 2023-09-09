using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Vintagestory.API.Client;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using System.Collections.Generic;
using RPVoiceChat.Utils;
using RPVoiceChat.Audio;

namespace RPVoiceChat
{
    struct AudioData
    {
        public byte[] Data;
        public int Length;
        public VoiceLevel VoiceLevel;
        public double Amplitude;
    }

    public class MicrophoneManager : IDisposable
    {
        public static int Frequency = 24000;
        public ALFormat InputFormat { get; private set; }
        private int BufferSize = (int)(Frequency * 0.5);
        private int channelCount;
        private const byte SampleToByte = 2;
        private double MaxInputThreshold;
        readonly ICoreClientAPI capi;

        private AudioCapture capture;
        private IAudioCodec codec;
        private RPVoiceChatConfig config;
        private ConcurrentQueue<AudioData> audioDataQueue = new ConcurrentQueue<AudioData>();
        private Thread audioProcessingThread;

        private VoiceLevel voiceLevel = VoiceLevel.Talking;
        public bool canSwitchDevice = true;
        public bool keyDownPTT = false;
        public bool Transmitting = false;
        public bool TransmittingOnPreviousStep = false;

        private long gameTickId = 0;

        private bool isRecording = false;
        private double inputThreshold;

        public double Amplitude { get; set; }
        public double AmplitudeAverage { get; set; }

        public ActivationMode CurrentActivationMode { get; private set; } = ActivationMode.VoiceActivation;
        public string CurrentInputDevice { get; internal set; }

        public event Action<byte[], int, VoiceLevel> OnBufferRecorded;
        public event Action<VoiceLevel> VoiceLevelUpdated;
        public event Action ClientStartTalking;
        public event Action ClientStopTalking;

        private List<double> recentAmplitudes = new List<double>();

        public MicrophoneManager(ICoreClientAPI capi)
        {
            audioProcessingThread = new Thread(ProcessAudio);
            this.capi = capi;
            config = ModConfig.Config;
            MaxInputThreshold = config.MaxInputThreshold;
            SetThreshold(config.InputThreshold);

            capture = CreateNewCapture(config.CurrentInputDevice);
            codec = new OpusCodec(Frequency, channelCount);
        }

        public void Launch()
        {
            audioProcessingThread.Start();
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
            audioProcessingThread?.Abort();
            capture?.Stop();
            capture?.Dispose();
        }

        public void SetThreshold(int threshold)
        {
            inputThreshold = (threshold / 100.0) * MaxInputThreshold;
        }

        public void UpdateCaptureAudioSamples(float deltaTime)
        {
            bool isMuted = config.IsMuted;
            var clientEntity = capi.World.Player?.Entity;

            if (isMuted || clientEntity == null || capture == null || !clientEntity.Alive || clientEntity.AnimManager.IsAnimationActive("sleep"))
                return;

            int samplesAvailable = capture.AvailableSamples;
            int bufferLength = samplesAvailable * SampleToByte * channelCount;
            if (samplesAvailable <= 0) return;

            // because we would have to copy, its actually faster to just allocate each time here.
            var sampleBuffer = new byte[bufferLength];
            capture.ReadSamples(sampleBuffer, samplesAvailable);

            // this adds some latency and cpu time to our clients, however, it allows for processing to be done before
            // we send off the data. It also ensure that the packets arrive in order, if we just used Task.Run()
            // we are not guaranteed an order of the packets finishing
            AudioData data = PreprocessRawAudio(sampleBuffer);
            audioDataQueue.Enqueue(data);
        }

        /// <summary>
        /// Converts audio to Mono16 and calculates amplitude
        /// </summary>
        private AudioData PreprocessRawAudio(byte[] rawSamples)
        {
            var monoSamplesCount = rawSamples.Length / channelCount;
            short[] monoSamples = new short[monoSamplesCount];

            var rawSampleSize = SampleToByte * channelCount;
            var monoSampleSize = SampleToByte;
            double sampleSquareSum = 0;

            for (var rawSampleIndex = 0; rawSampleIndex < rawSamples.Length; rawSampleIndex += rawSampleSize)
            {
                double monoSample = 0;

                int[] usedChannels = DetectAudioChannels(rawSamples);
                for (var channelIndex = 0; channelIndex < channelCount; channelIndex++)
                {
                    if (!usedChannels.Contains(channelIndex)) continue;
                    var sampleIndex = rawSampleIndex + channelIndex * monoSampleSize;
                    var sample = BitConverter.ToInt16(rawSamples, sampleIndex);
                    monoSample += sample;
                }
                monoSample = monoSample / usedChannels.Length;

                var monoSampleIndex = rawSampleIndex / rawSampleSize;
                monoSamples[monoSampleIndex] = (short)monoSample;

                sampleSquareSum += Math.Pow(monoSample / short.MaxValue, 2);
            }

            var numSamples = monoSamplesCount / SampleToByte;
            var amplitude = Math.Sqrt(sampleSquareSum / numSamples);

            byte[] opusEncodedAudio = codec.Encode(monoSamples);

            return new AudioData()
            {
                Data = opusEncodedAudio,
                Length = opusEncodedAudio.Length,
                VoiceLevel = voiceLevel,
                Amplitude = amplitude
            };
        }

        private void ProcessAudio()
        {
            while (audioProcessingThread.IsAlive)
            {
                if (!audioDataQueue.TryDequeue(out var data))
                {
                    Thread.Sleep(30);
                    continue;
                }

                Amplitude = data.Amplitude;
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
                    OnBufferRecorded?.Invoke(data.Data, data.Length, data.VoiceLevel);
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

        private AudioCapture CreateNewCapture(string deviceName, ALFormat? captureFormat = null)
        {
            ALFormat format = captureFormat ?? GetDefaultInputFormat();
            if (capture?.CurrentDevice == deviceName && capture?.SampleFormat == format)
                return capture;

            capture?.Stop();
            capture?.Dispose();
            SetInputFormat(format);

            AudioCapture newCapture = null;
            try
            {
                newCapture = new AudioCapture(deviceName, Frequency, InputFormat, BufferSize);
            }
            catch
            {
                Logger.client.Error("Could not create audio capture device, is there any microphone plugged in?");
            }
            config.CurrentInputDevice = deviceName;
            ModConfig.Save(capi);

            return newCapture;
        }

        private ALFormat GetDefaultInputFormat()
        {
            var format = ALFormat.Mono16;
            var supportedFormats = AL.Get(ALGetString.Extensions);
            if (supportedFormats.Contains("AL_EXT_MCFORMATS"))
                format = ALFormat.MultiQuad16Ext;

            return format;
        }

        private void SetInputFormat(ALFormat format)
        {
            switch (format)
            {
                case ALFormat.Mono16:
                    InputFormat = format;
                    channelCount = 1;
                    break;
                case ALFormat.MultiQuad16Ext:
                    InputFormat = format;
                    channelCount = 4;
                    break;
                default:
                    throw new NotSupportedException($"Format {format} is not supported for capture");
            }
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
            var sampleSums = new int[channelCount];

            var rawSampleSize = SampleToByte * channelCount;
            int monoSampleSize = SampleToByte;

            for (var rawSampleIndex = 0; rawSampleIndex < rawSampleSize*depth; rawSampleIndex += rawSampleSize)
            {
                for (var channelIndex = 0; channelIndex < channelCount; channelIndex++)
                {
                    var sampleIndex = rawSampleIndex + channelIndex * monoSampleSize;
                    int sample = BitConverter.ToInt16(rawSamples, sampleIndex);
                    sampleSums[channelIndex] += Math.Abs(sample);
                }
            }

            for (var channelIndex = 0; channelIndex < channelCount; channelIndex++)
            {
                var averageSampleValue = sampleSums[channelIndex] / depth;
                if (averageSampleValue > 5) usedChannels.Add(channelIndex);
            }

            return usedChannels.ToArray();
        }

        public string[] GetInputDeviceNames()
        {
            return AudioCapture.AvailableDevices.ToArray();
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

        public void SetVoiceLevel(VoiceLevel level)
        {
            voiceLevel = level;
            VoiceLevelUpdated?.Invoke(voiceLevel);
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
