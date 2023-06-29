using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Concurrent;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace rpvoicechat
{
    public class RPAudioOutputManager
    {
        ICoreClientAPI clientApi;
        WaveOut waveOut;
        MixingSampleProvider mixer;

        ConcurrentDictionary<string, BufferedWaveProvider> playerBuffers = new ConcurrentDictionary<string, BufferedWaveProvider>();

        public RPAudioOutputManager(ICoreClientAPI clientApi)
        {
            this.clientApi = clientApi;

            waveOut = new WaveOut();
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioUtils.sampleRate, 2));
            mixer.ReadFully = true;

            waveOut.Init(mixer);
            waveOut.Play();
        }

        public void HandleAudioPacket(PlayerAudioPacket packet)
        {
            Vec3d audioPos = packet.audioPos;

            // Turn the audio data into positional audio
            // TODO: Make this only run if the source of the audio is a player voice (Allow for stuff like radios or phones or something)
            short[] audioOutput = AudioUtils.MakePositionalAudio(clientApi.World.Player.Entity.Pos, audioPos, packet.audioData, packet.voiceLevel);

            // Apply muffling to the audio (Occlusion checks done in method)
            audioOutput = AudioUtils.ApplyMuffling(audioOutput, clientApi.World, audioPos);

            // Make audio final stereo audio bytes
            byte[] finalAudio = AudioUtils.ConvertShortsToBytes(audioOutput);

            // Add the audio to the bufferedWaveProvider of the player
            //audioOutputManager.AddSample(packet.playerUid, finalAudio);
            AddSample(packet.playerUid, finalAudio);
        }

        private void AddSample(string playerUid, byte[] samples)
        {
            if (!playerBuffers.ContainsKey(playerUid))
            {
                BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioUtils.sampleRate, 2));
                bufferedWaveProvider.DiscardOnBufferOverflow = true;
                playerBuffers.TryAdd(playerUid, bufferedWaveProvider);
                mixer.AddMixerInput(bufferedWaveProvider);
            }

            clientApi.Logger.Debug("Adding sample to player buffer");
            playerBuffers[playerUid].AddSamples(samples, 0, samples.Length);

            clientApi.Logger.Debug("BufferedWaveProvider buffer length: " + playerBuffers[playerUid].BufferLength);
        }

        public void RemovePlayer(string playerUid)
        {
            if (playerBuffers.ContainsKey(playerUid))
            {
                BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioUtils.sampleRate, 2));
                playerBuffers.TryRemove(playerUid, out bufferedWaveProvider);
                mixer.RemoveMixerInput((ISampleProvider)bufferedWaveProvider);
            }
        }
    }
}
