using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using Vintagestory.API.Common.Entities;
using System.Collections.Concurrent;

namespace rpvoicechat
{
    public class AudioOutputManager
    {
        ICoreClientAPI capi;
        EntityPos _listenerPos;
        MixingSampleProvider _mixer;
        WaveOut waveOut;



        private ConcurrentDictionary<string, PlayerAudioSource> _playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();

        public AudioOutputManager(ICoreClientAPI api)
        {
            capi = api;
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioUtils.sampleRate, 2));
            _mixer.ReadFully = true;
            waveOut = new WaveOut();
            waveOut.Init(_mixer);
            waveOut.Play();
        }

        public void SetListenerPosition(EntityPos pos)
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
            _mixer.AddMixerInput(playerAudioSource.Buffer.ToSampleProvider());
            _mixer.AddMixerInput(playerAudioSource.ReverbBuffer.ToSampleProvider());
        }

        public void RemovePlayer(IPlayer player)
        {
            if (_playerSources.TryGetValue(player.PlayerUID, out PlayerAudioSource source))
            {
                _mixer.RemoveMixerInput(source.Buffer.ToSampleProvider());
                _mixer.RemoveMixerInput(source.ReverbBuffer.ToSampleProvider());
                _playerSources.TryRemove(player.PlayerUID, out PlayerAudioSource val);
            }
        }

        // Updated every 20 milliseconds per player
        public async Task UpdatePlayerSource(IPlayer player)
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

                // Set if is locational
                source.IsLocational = !(source.Position == _listenerPos.XYZ || player.PlayerUID == capi.World.Player.PlayerUID || source.Position == null);

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
                    if (!source.AudioQueue.TryDequeue(out AudioPacket packet))
                        continue;

                    var audioData = packet.AudioData;

                    // If the audio is muffled, we need to apply the muffling effect
                    if (source.IsMuffled)
                        audioData = AudioUtils.ApplyMuffling(audioData);

                    // If the audio is reverberated, we need to apply the reverb effect
                    // TODO: Make this work
                    if (source.IsReverberated)
                        source.ReverbEffect.ProcessAudioStream(audioData);

                    // If the audio is locational, we need to apply the distance and stereo effects
                    // Otherwise, we just need to make it stereo
                    if (source.IsLocational)
                    {
                        audioData = AudioUtils.VolumeBasedOnDistance(audioData, _listenerPos.DistanceTo(source.Position), packet.voiceLevel);
                        audioData = AudioUtils.MakeStereoFromMono(audioData, _listenerPos, source.Position);
                    }
                    else
                    {
                        audioData = AudioUtils.MakeStereoFromMono(audioData);
                    }

                    source.Buffer.AddSamples(audioData, 0, audioData.Length);

                }

                /*
                Task.Run(() =>
                {
                    var reverbLocation = source.Position;
                    var muffled = source.IsMuffled;
                    while (source.ReverbEffect.ReverbQueue.Count > 0)
                    {
                        if (!source.ReverbEffect.ReverbQueue.TryDequeue(out var audioData))
                            continue;

                        // If the audio is muffled, we need to apply the muffling effect
                        if (muffled)
                            audioData = AudioUtils.ApplyMuffling(audioData);


                        audioData = AudioUtils.VolumeBasedOnDistance(audioData, _listenerPos.DistanceTo(reverbLocation), VoiceLevel.Shouting);
                        audioData = AudioUtils.MakeStereoFromMono(_listenerPos, reverbLocation, audioData);


                        source.ReverbBuffer.AddSamples(audioData, 0, audioData.Length);
                    }
                });
                */
            }
        }

        public void ClearAudio()
        {
            foreach (var source in _playerSources.Values)
            {
                source.Buffer.ClearBuffer();
            }
        }
    }
}

