using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using System.Threading.Tasks;
using Vintagestory.API.Common.Entities;
using System.Collections.Concurrent;
using System.Linq;
using OpenTK.Audio.OpenAL;
using rpvoicechat.src.Utils;
using Vintagestory.API.Util;

namespace rpvoicechat
{
    public class AudioOutputManager
    {
        ICoreClientAPI capi;
        EntityPos _listenerPos;
        MixingSampleProvider _mixer;
        WaveOut waveOut;
        RPVoiceChatConfig _config;



        private ConcurrentDictionary<string, PlayerAudioSource> _playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();

        public AudioOutputManager(ICoreClientAPI api)
        {
            capi = api;
            _config = ModConfig.config;

            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioUtils.sampleRate, 2));
            _mixer.ReadFully = true;

            waveOut = CreateNewWaveOut(_config.CurrentOutputDeviceIndex);
            
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

        // Ran every 20 milliseconds
        public void PlayAudio()
        {
            
        }

        public void ClearAudio()
        {
            
        }

        public WaveOutCapabilities CycleOutputDevice()
        {
            int deviceCount = WaveOut.DeviceCount;
            int currentDevice = waveOut.DeviceNumber;
            int nextDevice = currentDevice + 1;

            if (nextDevice >= deviceCount)
            {
                nextDevice = 0;
            }

            waveOut = CreateNewWaveOut(nextDevice);

            return WaveOut.GetCapabilities(nextDevice);
        }

        public string[] GetInputDeviceIds()
        {
            string[] deviceIds = new string[WaveOut.DeviceCount];
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                deviceIds[i] = i.ToString();
            }
            return deviceIds;
        }

        public string[] GetInputDeviceNames()
        {
            IList<string> deviceNames = Alc.GetString(IntPtr.Zero, AlcGetStringList.AllDevicesSpecifier);
            return deviceNames.Distinct().ToArray();
        }


        public WaveOut CreateNewWaveOut(int deviceIndex)
        {
            if (waveOut?.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Stop();
            }
            waveOut?.Dispose();

            waveOut = new WaveOut();
            _config.CurrentOutputDeviceIndex = deviceIndex;
            ModConfig.Save(capi);
            waveOut.DeviceNumber = deviceIndex;
            waveOut.Init(_mixer);
            waveOut.Play();

            return waveOut;
        }

        public void SetOutputDevice(string deviceId)
        {
            int device = deviceId.ToInt(0);
            waveOut = CreateNewWaveOut(device);
        }

        private int GetVoiceDistance(VoiceLevel voiceLevel)
        {
            switch (voiceLevel)
            {
                case VoiceLevel.Whispering:
                    return capi.World.Config.GetInt("rpvoicechat:distance-whisper", 5);
                case VoiceLevel.Normal:
                    return capi.World.Config.GetInt("rpvoicechat:distance-talk", 15);
                case VoiceLevel.Shouting:
                    return capi.World.Config.GetInt("rpvoicechat:distance-shout", 25);
                default:
                    return 15;
            }
        }

    }
}

