using NAudio;
using NAudio.Wave;
using System;
using System.Threading.Tasks;
using Vintagestory.API.Client;

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

        WaveInEvent waveIn;
        RPVoiceChatSocketClient socket;
        private ICoreClientAPI clientApi;
        private VoiceLevel voiceLevel = VoiceLevel.Normal;
        public bool isRecording = false;
        public bool isMuted = false;
        public bool isPushToTalkActive = false;
        public float inputThreshold = 0.0005f;
        private bool canSwitchDevice = true;
        private int ignoreThresholdCounter = 0;
        private readonly object recordingLock = new object();
        private const int ignoreThresholdLimit = 5;
        public ActivationMode CurrentActivationMode { get; private set; } = ActivationMode.VoiceActivation;
        public TalkingState CurrentTalkingState { get; private set; } = TalkingState.Empty;

        public RPAudioInputManager(RPVoiceChatSocketClient socket, ICoreClientAPI clientAPI)
        {
            this.socket = socket;
            this.clientApi = clientAPI;

            waveIn = new WaveInEvent();
            waveIn.WaveFormat = new WaveFormatMono();
            waveIn.BufferMilliseconds = 20;
            waveIn.DataAvailable += OnAudioRecorded;
            waveIn.StartRecording();
        }

        private void OnAudioRecorded(object sender, WaveInEventArgs args)
        {
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
            float amplitude = AudioUtils.CalculateAmplitude(args.Buffer, args.BytesRecorded);

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

            // Create a new audio packet
            PlayerAudioPacket packet = new PlayerAudioPacket() { audioData = args.Buffer, audioPos = clientApi.World.Player.Entity.Pos.XYZ, playerUid = clientApi.World.Player.PlayerUID, voiceLevel = voiceLevel };

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
                    waveIn.StopRecording();
                }
                else
                {
                    waveIn.StartRecording();
                }

                isRecording = mode;

                canSwitchDevice = true;
                return isRecording;
            }
        }

        public void SetInputDevice(int deviceIndex)
        {
            waveIn.DeviceNumber = deviceIndex;
        }

        public WaveInCapabilities CycleInputDevice()
        {
            ToggleRecording(false);
            int deviceCount = WaveIn.DeviceCount;
            int currentDevice = waveIn.DeviceNumber;
            int nextDevice = currentDevice + 1;

            if (nextDevice >= deviceCount)
            {
                nextDevice = 0;
            }

            SetInputDevice(nextDevice);
            ToggleRecording(true);

            

            return WaveIn.GetCapabilities(nextDevice);
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
