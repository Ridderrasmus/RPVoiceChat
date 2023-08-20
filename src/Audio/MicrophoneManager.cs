using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Vintagestory.API.Client;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using System.Collections.Generic;

namespace rpvoicechat
{
    struct AudioData
    {
        public byte[] Data;
        public int Length;
        public VoiceLevel VoiceLevel;
    }

    public class MicrophoneManager : IDisposable
    {
        public static int Frequency = 44100;
        public static int BufferSize = (int)(Frequency * 0.5);
        const byte SampleToByte = 2;
        private double MaxInputThreshold;
        readonly ICoreClientAPI capi;

        private AudioCapture capture;
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
            capture = new AudioCapture(config.CurrentInputDevice, Frequency, ALFormat.Mono16, BufferSize);
        }

        public void Launch()
        {
            audioProcessingThread.Start();
            gameTickId = capi.Event.RegisterGameTickListener(UpdateCaptureAudioSamples, 100);
            capture.Start();
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
            audioProcessingThread.Abort();
            capture.Stop();
            capture.Dispose();
        }

        public void SetThreshold(int threshold)
        {
            inputThreshold = (threshold / 100.0) * MaxInputThreshold;
        }

        public void UpdateCaptureAudioSamples(float deltaTime)
        {
            bool isMuted = config.IsMuted;


            if (!capi.World.Player.Entity.Alive || capi.World.Player.Entity.AnimManager.IsAnimationActive("sleep"))
            {
                return;
            }

            if (isMuted)
            {
                return;
            }

            int samplesAvailable = capture.AvailableSamples;
            int bufferLength = samplesAvailable * SampleToByte;
            if (samplesAvailable <= 0)
            {
                return;
            }

            // because we would have to copy, its actually faster to just allocate each time here. 
            var sampleBuffer = new byte[bufferLength];
            capture.ReadSamples(sampleBuffer, samplesAvailable);
            

            // this adds some latency and cpu time to our clients, however, it allows for processing to be done before 
            // we send off the data. It also ensure that the packets arrive in order, if we just used Task.Run()
            // we are not guaranteed an order of the packets finishing
            audioDataQueue.Enqueue(new AudioData()
            {
                Data = sampleBuffer,
                Length = bufferLength,
                VoiceLevel = voiceLevel
            });
        }

        private void ProcessAudio()
        {
            while (audioProcessingThread.IsAlive)
            {
                if (!audioDataQueue.TryDequeue(out var data)) Thread.Sleep(30);

                
                double rms = 0;
                var numSamples = data.Length / SampleToByte;
                for (var i = 0; i < data.Length; i += SampleToByte)
                {
                    var sample = ((BitConverter.ToInt16(data.Data, i) / (double)short.MaxValue));
                    rms += sample*sample;
                }

                var calc = Math.Abs(Math.Sqrt(rms / numSamples));
                if (double.IsNaN(calc)) calc = 0.000001;
                Amplitude = calc;


                recentAmplitudes.Add(Amplitude);

                if (recentAmplitudes.Count > 20)
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

        private AudioCapture CreateNewCapture(string deviceName)
        {
            if (capture.CurrentDevice == deviceName)
            {
                return capture;
            }

            if (capture.IsRunning)
            {
                capture.Stop();
            }
            capture.Dispose();

            var newCapture = new AudioCapture(deviceName, Frequency, ALFormat.Mono16, BufferSize);
            config.CurrentInputDevice = deviceName;
            ModConfig.Save(capi);

            newCapture.Start();

            return newCapture;
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

            return voiceLevel;
        }

        public void SetInputDevice(string deviceId)
        {
            capture = CreateNewCapture(deviceId);
        }
    }
}
