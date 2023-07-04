using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace rpvoicechat
{
    


    public class MicrophoneManager
    {
        ICoreClientAPI capi;

        WaveInEvent capture;

        private VoiceLevel voiceLevel = VoiceLevel.Normal;
        public bool isGamePaused = false;
        public bool canSwitchDevice = true;
        public bool isMuted = false;
        public bool keyDownPTT = false;
        public bool pushToTalkEnabled = false;
        public int inputThreshold = 20;
        public IPlayer[] playersNearby;

        private bool isRecording = false;
        private int ignoreThresholdCounter = 0;
        private const int ignoreThresholdLimit = 8;

        public ActivationMode CurrentActivationMode { get; private set; } = ActivationMode.VoiceActivation;
        public string CurrentInputDevice { get; internal set; }

        public event Action<byte[], VoiceLevel> OnBufferRecorded;

        public MicrophoneManager(ICoreClientAPI capi)
        {
            this.capi = capi;
            capture = CreateNewCapture(0);
        }

        private void OnAudioRecorded(object sender, WaveInEventArgs e)
        {
            int validBytes = e.BytesRecorded;
            byte[] buffer = new byte[validBytes];
            Array.Copy(e.Buffer, buffer, validBytes);



            // If player is in the pause menu, return
            if (capi.IsGamePaused)
                return;

            if (capi.World == null)
                return;

            if (capi.World.Player == null)
                return;

            if (capi.World.Player.Entity == null)
                return;

            if (!capi.World.Player.Entity.Alive || capi.World.Player.Entity.AnimManager.IsAnimationActive("sleep"))
                return;

            if (isMuted)
                return;

            keyDownPTT = capi.Input.KeyboardKeyState[capi.Input.GetHotKeyByCode("voicechatPTT").CurrentMapping.KeyCode];
            if (pushToTalkEnabled && !keyDownPTT)
                return;

            // Get the amplitude of the audio
            int amplitude = AudioUtils.CalculateAmplitude(buffer, validBytes);

            // If the amplitude is below the threshold, return
            if (!pushToTalkEnabled && amplitude < inputThreshold)
            {
                if (ignoreThresholdCounter > 0)
                {
                    ignoreThresholdCounter--;
                }
                else
                {
                    return;
                }
            }
            else
            {
                ignoreThresholdCounter = ignoreThresholdLimit; // Reset the counter when amplitude is above the threshold
            }



            if (playersNearby?.Length <= 1)
                return;

            buffer = AudioUtils.HandleAudioPeaking(buffer);
            OnBufferRecorded?.Invoke(buffer, voiceLevel);
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
                capture.StopRecording();
            }
            else
            {
                capture.StartRecording();
            }

            isRecording = mode;

            canSwitchDevice = true;
            return isRecording;
        }


        public WaveInCapabilities CycleInputDevice()
        {

            int deviceCount = WaveIn.DeviceCount;
            int currentDevice = capture.DeviceNumber;
            int nextDevice = currentDevice + 1;

            if (nextDevice >= deviceCount)
            {
                nextDevice = 0;
            }

            capture = CreateNewCapture(nextDevice);

            return WaveIn.GetCapabilities(nextDevice);
        }

        private WaveInEvent CreateNewCapture(int deviceIndex)
        {
            if (isRecording)
            {
                capture?.StopRecording();
            }
            capture?.Dispose();

            WaveFormat customWaveFormat = new WaveFormatMono();

            WaveInEvent newCapture = new WaveInEvent();
            newCapture.DeviceNumber = deviceIndex;
            newCapture.WaveFormat = customWaveFormat;
            newCapture.BufferMilliseconds = 30;
            newCapture.DataAvailable += OnAudioRecorded;

            newCapture.StartRecording();

            return newCapture;
        }

        public WaveInEvent CreateNewCapture()
        {
            return CreateNewCapture(0);
        }

        public int GetInputDeviceCount()
        {
            return WaveIn.DeviceCount;
        }

        public string GetInputDeviceName()
        {
            return WaveIn.GetCapabilities(capture.DeviceNumber).ProductName;
        }

        public string[] GetInputDeviceNames()
        {
            List<string> deviceNames = new List<string>();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                deviceNames.Add(WaveIn.GetCapabilities(i).ProductName);
            }
            return deviceNames.ToArray();
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

        public string[] GetInputDeviceIds()
        {
            string[] deviceIds = new string[WaveIn.DeviceCount];
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                deviceIds[i] = i.ToString();
            }
            return deviceIds;
        }

        internal void SetInputDevice(string deviceId)
        {
            int device = deviceId.ToInt(0);
            capture = CreateNewCapture(device);
        }
    }
}
