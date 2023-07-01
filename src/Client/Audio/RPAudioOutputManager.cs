using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace rpvoicechat
{
    public class RPAudioOutputManager
    {
        ICoreClientAPI clientApi;
        private ConcurrentDictionary<string, PlayerAudioSource> _playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();


        public RPAudioOutputManager(ICoreClientAPI clientApi)
        {
            this.clientApi = clientApi;

        }

        public void HandleAudioPacket(PlayerAudioPacket packet)
        {

            PlayAudioForPlayer(packet);
        }

        public void SetPlayerMuffled(string playerUid, bool isMuffled)
        {
            if (_playerSources.TryGetValue(playerUid, out PlayerAudioSource source))
            {
                source.isMuffled = isMuffled;
            }
        }


        public void AddPlayer(string playerUid, Vec3d initialPosition)
        {
            // Generate a source for the player
            PlayerAudioSource source = new PlayerAudioSource(initialPosition, false);
            AL.Source(source.sourceNum, ALSource3f.Position, (float)initialPosition.X, (float)initialPosition.Y, (float)initialPosition.Z);

            // Store it in the dictionary
            _playerSources[playerUid] = source;
        }

        public void RemovePlayer(string playerUid)
        {
            // Delete the source associated with the player
            if (_playerSources.TryGetValue(playerUid, out PlayerAudioSource source))
            {
                AL.DeleteSource(source.sourceNum);
                _playerSources.TryRemove(playerUid, out _);
            }
        }

        public void UpdatePlayerSource(string playerUid, Vec3d newPosition)
        {
            if(newPosition.DistanceTo(clientApi.World.Player.Entity.Pos.XYZ) > ((float)VoiceLevel.Shout + 10))
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
                AL.Source(source.sourceNum, ALSource3f.Position, (float)newPosition.X, (float)newPosition.Y, (float)newPosition.Z);
            }
        }

        public void PlayAudioForPlayer(PlayerAudioPacket packet)
        {
            string playerUid = packet.playerUid;
            byte[] audioData = packet.audioData;

            if (_playerSources.TryGetValue(playerUid, out PlayerAudioSource source))
            {
                if (source.isMuffled)
                    audioData = AudioUtils.ApplyMuffling(audioData);

                // Load audio data into buffer
                int buffer = AL.GenBuffer();
                AL.BufferData(buffer, ALFormat.Mono16, audioData, audioData.Length, AudioUtils.sampleRate);

                // Attach buffer to source and play
                AL.Source(source.sourceNum, ALSourcei.Buffer, buffer);
                AL.SourcePlay(source.sourceNum);

                // Optionally delete buffer if it's not going to be reused
                AL.DeleteBuffer(buffer);
            }
        }

    }
}
