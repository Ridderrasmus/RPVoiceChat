using NAudio.Wave;
using System.Threading.Tasks;
using Vintagestory.API.Client;

namespace rpvoicechat
{
    public class RPAudioInputManager
    {
        WaveInEvent waveIn;
        RPVoiceChatSocketClient socket;
        private ICoreClientAPI clientApi;
        private VoiceLevel voiceLevel = VoiceLevel.Normal;
        public bool isRecording = false;
        public float inputThreshold = 0.001f;

        public RPAudioInputManager(RPVoiceChatSocketClient socket, ICoreClientAPI clientAPI)
        {
            this.socket = socket;
            this.clientApi = clientAPI;

            waveIn = new WaveInEvent();
            waveIn.WaveFormat = new WaveFormatMono();
            waveIn.BufferMilliseconds = 20;
            waveIn.DataAvailable += OnAudioRecorded;
            StartRecording();
        }

        private void OnAudioRecorded(object sender, WaveInEventArgs args)
        {
            // If player is in the pause menu, return
            if (clientApi.IsGamePaused) return;
            
            // Get the amplitude of the audio
            float amplitude = AudioUtils.CalculateAmplitude(args.Buffer, args.BytesRecorded);

            // If the amplitude is below the threshold, return
            if (amplitude < inputThreshold) return;

            // Create a new audio packet
            PlayerAudioPacket packet = new PlayerAudioPacket() { audioData = args.Buffer, audioPos = clientApi.World.Player.Entity.Pos.XYZ, playerUid = clientApi.World.Player.PlayerUID };
            
            socket.SendAudioPacket(packet);
        }

        public void StartRecording()
        {
            if (isRecording) return;
            isRecording = true;
            waveIn.StartRecording();
        }

        public void StopRecording()
        {
            if (!isRecording) return;
            isRecording = false;
            waveIn.StopRecording();
        }

        public void SetInputDevice(int deviceIndex)
        {
            waveIn.DeviceNumber = deviceIndex;
        }

        public WaveInCapabilities CycleInputDevice()
        {
            StopRecording();
            int deviceCount = WaveIn.DeviceCount;
            int currentDevice = waveIn.DeviceNumber;
            int nextDevice = currentDevice + 1;

            if (nextDevice >= deviceCount)
            {
                nextDevice = 0;
            }

            SetInputDevice(nextDevice);
            StartRecording();

            

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


    }
}
