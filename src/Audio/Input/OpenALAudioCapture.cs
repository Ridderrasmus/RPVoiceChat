using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPVoiceChat.Audio
{
    public class OpenALAudioCapture : IAudioCapture
    {
        public int AvailableSamples { get { return audioCapture.AvailableSamples; } }
        public string CurrentDevice { get { return audioCapture.CurrentDevice; } }
        public ALFormat SampleFormat { get { return audioCapture.SampleFormat; } }
        private AudioCapture audioCapture;

        public OpenALAudioCapture(string deviceName, int frequency, ALFormat format, int bufferSize)
        {
            if (deviceName == "Default") deviceName = null;

            try
            {
                audioCapture = new AudioCapture(deviceName, frequency, format, bufferSize);
                return;
            }
            catch (Exception e)
            {
                Logger.client.VerboseDebug($"[Internal] Failed to open capture device {deviceName}, {frequency}, {format}, {bufferSize}: {e}");
            }

            audioCapture = new AudioCapture(deviceName, frequency, ALFormat.Mono16, bufferSize);
        }

        public void Start()
        {
            audioCapture.Start();
        }

        public void Stop()
        {
            audioCapture.Stop();
        }

        public void ReadSamples(byte[] buffer, int count)
        {
            audioCapture.ReadSamples(buffer, count);
        }

        public static List<string> GetAvailableDevices()
        {
            return AudioCapture.AvailableDevices.ToList();
        }

        public void Dispose()
        {
            audioCapture?.Dispose();
        }
    }
}
