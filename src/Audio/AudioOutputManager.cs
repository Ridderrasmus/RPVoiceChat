using OpenTK.Audio.OpenAL;
using OpenTK.Audio;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using NAudio.Wave;

namespace rpvoicechat
{
    public class AudioOutputManager
    {
        ICoreClientAPI clientApi;
        private ConcurrentDictionary<string, PlayerAudioSource> _playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();


        public AudioOutputManager(ICoreClientAPI clientApi)
        {
            this.clientApi = clientApi;
        }

        public void HandleAudioPacket(AudioPacket packet)
        {
            string playerUid = packet.PlayerId;
            byte[] audioData = packet.AudioData;

            

            if (_playerSources.TryGetValue(playerUid, out PlayerAudioSource source))
            {
                if(source.IsMuffled)
                    audioData = AudioUtils.ApplyMuffling(audioData);


                if (source.AudioQueue.Count < 10)
                {
                    source.AudioQueue.Enqueue(audioData);
                }
            }
            
        }

        public void SetPlayerMuffled(string playerUid, bool isMuffled)
        {
            if (_playerSources.TryGetValue(playerUid, out PlayerAudioSource source))
            {
                source.IsMuffled = isMuffled;
                _playerSources[playerUid] = source;
            }
        }


        public void AddPlayer(string playerUid, Vec3d initialPosition)
        {
            // Generate a source for the player
            PlayerAudioSource source = new PlayerAudioSource(initialPosition, false);
            AL.Source(source.SourceNum, ALSource3f.Position, (float)initialPosition.X, (float)initialPosition.Y, (float)initialPosition.Z);

            // Store it in the dictionary
            _playerSources[playerUid] = source;
        }

        public void RemovePlayer(string playerUid)
        {
            // Delete the source associated with the player
            if (_playerSources.TryGetValue(playerUid, out PlayerAudioSource source))
            {
                AL.DeleteBuffer(source.BufferNum);
                AL.DeleteSource(source.SourceNum);
                _playerSources.TryRemove(playerUid, out _);
            }
        }

        public void UpdatePlayerSource(string playerUid, Vec3d newPosition)
        {
            if (newPosition.DistanceTo(clientApi.World.Player.Entity.Pos.XYZ) > ((float)VoiceLevel.Shouting + 10))
            {
                RemovePlayer(playerUid);
                return;
            }
            else if (!_playerSources.ContainsKey(playerUid))
            {
                AddPlayer(playerUid, newPosition);
                return;
            }

            // Update the source position for the player
            if (_playerSources.TryGetValue(playerUid, out PlayerAudioSource source))
            {
                AL.Source(source.SourceNum, ALSource3f.Position, (float)newPosition.X, (float)newPosition.Y, (float)newPosition.Z);
            }
        }

        public void UpdateAudio()
        {
            foreach (var playerSource in _playerSources.Values)
            {
                // Check if there's any audio data in the player's queue
                int queuedBuffers;
                AL.GetSource(playerSource.SourceNum, ALGetSourcei.BuffersQueued, out queuedBuffers);
                while (playerSource.AudioQueue.Count > 0 && queuedBuffers < 3)
                {
                    AL.GetSource(playerSource.SourceNum, ALGetSourcei.BuffersQueued, out queuedBuffers);
                    // Get next audio data from player's queue
                    byte[] audioData = playerSource.AudioQueue.Dequeue();

                    // Create a new buffer and load the audio data into it
                    int buffer = AL.GenBuffer();
                    AL.BufferData(buffer, ALFormat.Mono16, audioData, audioData.Length, AudioUtils.sampleRate);

                    // Queue the buffer to the source
                    AL.SourceQueueBuffer(playerSource.SourceNum, buffer);
                }

                // Check if any buffers have finished playing
                int buffersProcessed;
                AL.GetSource(playerSource.SourceNum, ALGetSourcei.BuffersProcessed, out buffersProcessed);
                for (int i = 0; i < buffersProcessed; i++)
                {
                    // Unqueue the buffer and delete it
                    int buffer = AL.SourceUnqueueBuffer(playerSource.SourceNum);
                    AL.DeleteBuffer(buffer);
                }

                // Check if the source is not currently playing and there are no more buffers queued
                int buffersQueued;
                AL.GetSource(playerSource.SourceNum, ALGetSourcei.BuffersQueued, out buffersQueued);
                if (AL.GetSourceState(playerSource.SourceNum) != ALSourceState.Playing && buffersQueued == 0)
                {
                    // Stop the source
                    AL.SourceStop(playerSource.SourceNum);
                }
                else if (AL.GetSourceState(playerSource.SourceNum) != ALSourceState.Playing)
                {
                    // Start playing the source
                    AL.SourcePlay(playerSource.SourceNum);
                }
            }
        }

        public void ClearAllAudio()
        {
            foreach (var playerSource in _playerSources.Values)
            {
                // Check if any buffers have been queued
                int buffersQueued; 
                AL.GetSource(playerSource.SourceNum, ALGetSourcei.BuffersQueued, out buffersQueued);
                for (int i = 0; i < buffersQueued; i++)
                {
                    // Unqueue the buffer and delete it
                    int buffer = AL.SourceUnqueueBuffer(playerSource.SourceNum);
                    AL.DeleteBuffer(buffer);
                }

                // Clear the player's audio queue
                playerSource.AudioQueue.Clear();

                // Stop the source
                AL.SourceStop(playerSource.SourceNum);
            }
        }
    }
}

