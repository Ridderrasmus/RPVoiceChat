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

namespace rpvoicechat
{
    public class RPAudioInputManager
    {
        public enum ActivationMode
        {
            VoiceActivation,
            PushToTalk
        }

        public enum TalkingState
        {
            Empty,
            Talking,
            Muted,
            Deafened
        }

        WaveInEvent capture;
        private int currentInputDeviceIndex = 0;

        RPVoiceChatSocketClient socket;
        private ICoreClientAPI clientApi;
        private VoiceLevel voiceLevel = VoiceLevel.Normal;
        public bool isRecording = false;
        public bool canSwitchDevice = true;
        public bool isMuted = false;
        public bool isPushToTalkActive = false;
        public double inputThreshold = 0.0005f;
        public IPlayer[] playersNearby;

        private int ignoreThresholdCounter = 0;
        private object recordingLock;
        private const int ignoreThresholdLimit = 20;

        
        public ActivationMode CurrentActivationMode { get; private set; } = ActivationMode.VoiceActivation;
        public TalkingState CurrentTalkingState { get; private set; } = TalkingState.Empty;

        public RPAudioInputManager(RPVoiceChatSocketClient socket, ICoreClientAPI clientAPI)
        {
            this.socket = socket;
            this.clientApi = clientAPI;

            capture = CreateNewCapture();

        }

        private void OnAudioRecorded(object sender, WaveInEventArgs e)
        {
            int validBytes = e.BytesRecorded;
            byte[] buffer = new byte[validBytes];
            Array.Copy(e.Buffer, buffer, validBytes);

            // If player is in the pause menu, return
            if (clientApi.IsGamePaused)
            {
                CurrentTalkingState = TalkingState.Empty;
                return;
            }

            if (isMuted)
            {
                CurrentTalkingState = TalkingState.Muted;
                return;
            }

            if (CurrentActivationMode == ActivationMode.PushToTalk && !isPushToTalkActive)
            { 
                CurrentTalkingState = TalkingState.Empty;
                return;
            }

            // Get the amplitude of the audio
            double amplitude = AudioUtils.CalculateAmplitude(buffer, validBytes);

            // If the amplitude is below the threshold, return
            if (CurrentActivationMode == ActivationMode.VoiceActivation && amplitude < inputThreshold)
            {
                if (ignoreThresholdCounter > 0)
                {
                    ignoreThresholdCounter--;
                }
                else
                {
                    CurrentTalkingState = TalkingState.Empty;
                    return;
                }
            }
            else
            {
                ignoreThresholdCounter = ignoreThresholdLimit; // Reset the counter when amplitude is above the threshold
            }

            
            if (playersNearby != null && playersNearby.Length < 1)
            {
                CurrentTalkingState = TalkingState.Talking;
                return;
            }

            // Create a new audio packet
            PlayerAudioPacket packet = new PlayerAudioPacket() { audioData = buffer, audioPos = clientApi.World.Player.Entity.Pos.XYZ, playerUid = clientApi.World.Player.PlayerUID, voiceLevel = voiceLevel };

            socket.SendAudioPacket(packet);
            CurrentTalkingState = TalkingState.Talking;
        }


        // Returns the success of the method
        public bool ToggleRecording()
        {
            lock (recordingLock)
            {
                return (ToggleRecording(!isRecording) == !isRecording);
            }
        }

        // Returns the recording status
        public bool ToggleRecording(bool mode)
        {
            if (!canSwitchDevice) return isRecording;
            canSwitchDevice = false;
            lock (recordingLock)
            {
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
                capture.StopRecording();
            }
            capture?.Dispose();

            WaveFormat customWaveFormat = new WaveFormatMono();

            WaveInEvent newCapture = new WaveInEvent();
            newCapture.DeviceNumber = deviceIndex;
            newCapture.WaveFormat = customWaveFormat;
            newCapture.BufferMilliseconds = 20;
            newCapture.DataAvailable += OnAudioRecorded;

            newCapture.StartRecording();

            return newCapture;
        }

        private WaveInEvent CreateNewCapture()
        {
            return CreateNewCapture(0);
        }



        public VoiceLevel CycleVoiceLevel()
        {
            if (voiceLevel == VoiceLevel.Normal)
            {
                voiceLevel = VoiceLevel.Shout;
            }
            else if (voiceLevel == VoiceLevel.Shout)
            {
                voiceLevel = VoiceLevel.Whisper;
            }
            else if (voiceLevel == VoiceLevel.Whisper)
            {
                voiceLevel = VoiceLevel.Normal;
            }

            return voiceLevel;
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
        }

        public void TogglePushToTalk(bool isActive)
        {
            isPushToTalkActive = isActive;
        }

        public void SetActivationMode(ActivationMode mode)
        {
            CurrentActivationMode = mode;
        }
    }
}
