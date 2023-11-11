using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;

namespace RPVoiceChat.Audio
{
    public class OpenALAudioCapture : IAudioCapture
    {
        public int AvailableSamples
        {
            get
            {
                ALC.GetInteger(_captureDevice, AlcGetInteger.CaptureSamples, 1, out int result);
                return result;
            }
        }
        public string CurrentDevice { get; private set; }
        public int Frequency { get; }
        public ALFormat SampleFormat { get; private set; }
        public int BufferSize { get; }
        public static string DefaultDevice = ALC.GetString(ALDevice.Null, AlcGetString.CaptureDefaultDeviceSpecifier);
        private ALCaptureDevice _captureDevice;
        private bool IsDisposed = false;
        private bool IsRunning = false;

        public OpenALAudioCapture(string deviceName, int frequency, ALFormat captureFormat, int bufferSize)
        {
            if (deviceName == "Default") deviceName = null;
            Frequency = frequency;
            BufferSize = bufferSize;

            string[] deviceNames = new string[] { deviceName, null, DefaultDevice };
            ALFormat[] formats = new ALFormat[] { captureFormat, ALFormat.Mono16 };

            foreach (var device in deviceNames)
                foreach (var format in formats)
                    if (TryOpenDevice(device, format)) break;

            if (_captureDevice == IntPtr.Zero)
                throw new Exception("All attempts to open capture devices returned IntPtr.Zero");

            if (CheckError(_captureDevice))
                throw new Exception("Capture device returned an exception");
        }

        public void Start()
        {
            ALC.CaptureStart(_captureDevice);
            IsRunning = true;
        }

        public void Stop()
        {
            ALC.CaptureStop(_captureDevice);
            IsRunning = false;
        }

        public void ReadSamples(byte[] buffer, int count)
        {
            ALC.CaptureSamples(_captureDevice, buffer, count);
        }

        public static List<string> GetAvailableDevices()
        {
            var devices = ALC.GetString(ALDevice.Null, AlcGetStringList.CaptureDeviceSpecifier);

            return devices;
        }

        private bool TryOpenDevice(string deviceName, ALFormat format)
        {
            CurrentDevice = deviceName;
            SampleFormat = format;
            _captureDevice = ALC.CaptureOpenDevice(deviceName, Frequency, format, BufferSize);
            LogError();
            return _captureDevice != IntPtr.Zero;
        }

        private void LogError()
        {
            CheckError(ALCaptureDevice.Null);
        }

        private bool CheckError(ALCaptureDevice device)
        {
            var ALCError = ALC.GetError(new ALDevice(device));
            if (ALCError == AlcError.NoError) return false;

            Logger.client.VerboseDebug($"[Internal] Failed to open capture device: {ALCError}, {CurrentDevice ?? "Default"}, {Frequency}, {SampleFormat}, {BufferSize}");
            return true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool manual)
        {
            if (IsDisposed) return;

            if (_captureDevice != IntPtr.Zero)
            {
                if (IsRunning) Stop();

                ALC.CaptureCloseDevice(_captureDevice);
            }
            IsDisposed = true;
        }
    }
}
