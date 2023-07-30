using NAudio.Wave;
using System;
using System.Linq;
using OpenTK;
using Vintagestory.API.Client;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace rpvoicechat
{
    public class MicrophoneManager
    {
        private static int FREQUENCY = 44100;
        private static int BUFFER_SIZE = (int)(20 * FREQUENCY * .001);
        const byte SampleToByte = 4;
        private byte[] sampleBuffer;

        ICoreClientAPI capi;

        private AudioCapture capture;
        private RPVoiceChatConfig config;

        private VoiceLevel voiceLevel = VoiceLevel.Normal;
        public bool isTalking = false;
        public bool isGamePaused = false;
        public bool canSwitchDevice = true;
        public bool keyDownPTT = false;
        public bool playersNearby = false;

        private bool isRecording = false;
        private int ignoreThresholdCounter = 0;
        private const int ignoreThresholdLimit = 13;

        public ActivationMode CurrentActivationMode { get; private set; } = ActivationMode.VoiceActivation;
        public string CurrentInputDevice { get; internal set; }

        public event Action<byte[], int, VoiceLevel> OnBufferRecorded;

        public MicrophoneManager(ICoreClientAPI capi)
        {
            this.capi = capi;
            config = ModConfig.config;
            sampleBuffer = new byte[BUFFER_SIZE];
            capture = new AudioCapture(config.CurrentInputDevice, FREQUENCY, ALFormat.Mono16, BUFFER_SIZE);
        }

        public void UpdateCaptureAudioSamples()
        {
            bool pushToTalkEnabled = config.PushToTalkEnabled;
            bool isMuted = config.IsMuted;
            int inputThreshold = config.InputThreshold;


            // If player is in the pause menu, return
            if (capi.IsGamePaused)
            {
                isTalking = false;
                return;
            }
            
            if (capi.World == null)
            {
                isTalking = false;
                return;
            }

            if (capi.World.Player == null)
            {
                isTalking = false;
                return;
            }

            if (capi.World.Player.Entity == null)
            {
                isTalking = false;
                return;
            }

            if (!capi.World.Player.Entity.Alive || capi.World.Player.Entity.AnimManager.IsAnimationActive("sleep"))
            {
                isTalking = false;
                return;
            }

            if (isMuted)
            {
                isTalking = false;
                return;
            }

            keyDownPTT = capi.Input.KeyboardKeyState[capi.Input.GetHotKeyByCode("voicechatPTT").CurrentMapping.KeyCode];
            if (pushToTalkEnabled && !keyDownPTT)
            {
                isTalking = false;
                return;
            }

            //if (!playersNearby)
            //{
            //    return;
            //}

            int samplesAvailable = capture.AvailableSamples;
            int bufferLength = samplesAvailable * SampleToByte;
            if (samplesAvailable <= 0)
            {
                return;
            }

            if (sampleBuffer.Length < bufferLength)
            {
                sampleBuffer = new byte[bufferLength];
            }
            
            capture.ReadSamples(sampleBuffer, samplesAvailable);

            //// Get the amplitude of the audio
            //int amplitude = AudioUtils.CalculateAmplitude(buffer, validBytes);

            //// If the amplitude is below the threshold, return
            //if (!pushToTalkEnabled && amplitude < inputThreshold)
            //{
            //    if (ignoreThresholdCounter > 0)
            //    {
            //        ignoreThresholdCounter--;
            //    }
            //    else
            //    {
            //        isTalking = false;
            //        return;
            //    }
            //}
            //else
            //{
            //    ignoreThresholdCounter = ignoreThresholdLimit; // Reset the counter when amplitude is above the threshold
            //}

            isTalking = true;

            //buffer = AudioUtils.HandleAudioPeaking(buffer);
            OnBufferRecorded?.Invoke(sampleBuffer, bufferLength, voiceLevel);
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

            var newCapture = new AudioCapture(deviceName, FREQUENCY, ALFormat.Mono16, BUFFER_SIZE);
            config.CurrentInputDevice = deviceName;

            newCapture.Start();

            return newCapture;
        }

        public string[] GetInputDeviceNames()
        {
            return AudioCapture.AvailableDevices.ToArray();
        }

        public VoiceLevel CycleVoiceLevel()
        {
            if (voiceLevel == VoiceLevel.Normal)
            {
                voiceLevel = VoiceLevel.Shouting;
            }
            else if (voiceLevel == VoiceLevel.Shouting)
            {
                voiceLevel = VoiceLevel.Whispering;
            }
            else if (voiceLevel == VoiceLevel.Whispering)
            {
                voiceLevel = VoiceLevel.Normal;
            }

            return voiceLevel;
        }

        public void SetInputDevice(string deviceId)
        {
            capture = CreateNewCapture(deviceId);
        }
    }
}
