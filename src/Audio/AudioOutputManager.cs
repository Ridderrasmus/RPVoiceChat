using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace rpvoicechat
{
    public class AudioOutputManager
    {
        ICoreClientAPI capi;
        Vec3d _listenerPos;
        MixingSampleProvider _mixer;
        WaveOut waveOut;



        private Dictionary<string, PlayerAudioSource> _playerSources = new Dictionary<string, PlayerAudioSource>();

        public AudioOutputManager(ICoreClientAPI capi)
        {
            this.capi = capi;
            this._mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioUtils.sampleRate, 2));
            waveOut = new WaveOut();
            waveOut.Init(_mixer, true);
            waveOut.Play();
        }

        public void SetListenerPosition(Vec3d pos)
        {
            _listenerPos = pos;
        }

        // Called when the client receives an audio packet supplying the audio packet
        public void HandleAudioPacket(AudioPacket packet)
        {
            if (_playerSources.TryGetValue(packet.PlayerId, out PlayerAudioSource source))
            {
                source.AudioQueue.Enqueue(packet);
            }
        }

        
        public void AddPlayer(IPlayer player)
        {
            var playerAudioSource = new PlayerAudioSource(player.Entity.Pos.XYZ);
            _playerSources[player.PlayerUID] = playerAudioSource;
            _mixer.AddMixerInput(playerAudioSource.Buffer);
        }

        public void RemovePlayer(IPlayer player)
        {
            if (_playerSources.TryGetValue(player.PlayerUID, out PlayerAudioSource source))
            {
                _mixer.RemoveMixerInput((ISampleProvider)source.Buffer);
                _playerSources.Remove(player.PlayerUID);
            }
        }

        // Updated every 20 milliseconds per player
        public void UpdatePlayerSource(IPlayer player)
        {
            if (player.Entity.Pos.DistanceTo(_listenerPos) > ((float)VoiceLevel.Shouting + 10))
            {
                RemovePlayer(player);
                return;
            }
            else if (!_playerSources.ContainsKey(player.PlayerUID))
            {
                AddPlayer(player);
            }

            // Update the source position for the player
            if (_playerSources.TryGetValue(player.PlayerUID, out PlayerAudioSource source))
            {
                // Set the position
                source.Position = player.Entity.Pos.XYZ;

                // Set muffled state
                BlockSelection blockSelection = new BlockSelection();
                EntitySelection entitySelection = new EntitySelection();
                capi.World.RayTraceForSelection(player.Entity.Pos.XYZ, capi.World.Player.Entity.Pos.XYZ, ref blockSelection, ref entitySelection);

                source.IsMuffled = (blockSelection != null);

                // Set reverberated state
                Room room = capi.ModLoader.GetModSystem<RoomRegistry>().GetRoomForPosition(new BlockPos().Set(player.Entity.Pos.XYZ));
                source.IsReverberated = (room.SkylightCount < room.NonSkylightCount) && (room.Location.Volume > 60);

            }
        }

        // Ran every 20 milliseconds
        public void PlayAudio()
        {
            foreach (var source in _playerSources.Values)
            {
                while (source.AudioQueue.Count > 0)
                {
                    var packet = source.AudioQueue.Dequeue();
                    var audioData = packet.AudioData;

                    // If the audio is muffled, we need to apply the muffling effect
                    if (source.IsMuffled)
                        audioData = AudioUtils.ApplyMuffling(audioData);

                    // If the audio is reverberated, we need to apply the reverb effect
                    if (source.IsReverberated)
                        audioData = source.ReverbEffect.ApplyReverb(audioData);

                    // If the audio is locational, we need to apply the distance and stereo effects
                    // Otherwise, we just need to make it stereo
                    if (source.IsLocational)
                    {
                        audioData = AudioUtils.VolumeBasedOnDistance(audioData, _listenerPos.DistanceTo(source.Position), packet.voiceLevel);
                        audioData = AudioUtils.MakeStereoFromMono(_listenerPos, source.Position, audioData);
                    }
                    else
                    {
                        audioData = AudioUtils.MakeStereoFromMono(audioData);
                    }


                    source.Buffer.AddSamples(audioData, 0, audioData.Length);

                }
            }
        }
    }
}

