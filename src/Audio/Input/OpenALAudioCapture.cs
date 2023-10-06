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
                OALCW.GetInteger(_captureDevice, AlcGetInteger.CaptureSamples, 1, out int result);
                return result;
            }
        }
        public string CurrentDevice { get; }
        public int Frequency { get; }
        public ALFormat SampleFormat { get; }
        public int BufferSize { get; }
        public static string DefaultDevice = OALCW.GetString(ALDevice.Null, AlcGetString.CaptureDefaultDeviceSpecifier);
        private ALCaptureDevice _captureDevice;
        private bool IsDisposed = false;
        private bool IsRunning = false;

        public OpenALAudioCapture(string deviceName, int frequency, ALFormat format, int bufferSize)
        {
            CurrentDevice = deviceName == "Default" ? null : deviceName;
            Frequency = frequency;
            SampleFormat = format;
            BufferSize = bufferSize;

            _captureDevice = OALCW.CaptureOpenDevice(CurrentDevice, Frequency, SampleFormat, BufferSize);
            LogError();

            if (_captureDevice == IntPtr.Zero)
            {
                CurrentDevice = null;
                _captureDevice = OALCW.CaptureOpenDevice(CurrentDevice, Frequency, SampleFormat, BufferSize);
                LogError();
            }

            if (_captureDevice == IntPtr.Zero)
            {
                CurrentDevice = DefaultDevice;
                _captureDevice = OALCW.CaptureOpenDevice(CurrentDevice, Frequency, SampleFormat, BufferSize);
                LogError();
            }

            if (_captureDevice == IntPtr.Zero)
            {
                SampleFormat = ALFormat.Mono16;
                _captureDevice = OALCW.CaptureOpenDevice(CurrentDevice, Frequency, ALFormat.Mono16, BufferSize);
                LogError();
            }

            if (_captureDevice == IntPtr.Zero)
            {
                throw new Exception("All attempts to open capture devices returned IntPtr.Zero");
            }

            if (CheckError(_captureDevice))
                throw new Exception("Capture device returned an exception");
        }

        public void Start()
        {
            OALCW.CaptureStart(_captureDevice);
            IsRunning = true;
        }

        public void Stop()
        {
            OALCW.CaptureStop(_captureDevice);
            IsRunning = false;
        }

        public void ReadSamples(byte[] buffer, int count)
        {
            OALCW.CaptureSamples(_captureDevice, buffer, count);
        }

        public static List<string> GetAvailableDevices()
        {
            var devices = OALCW.GetString(ALDevice.Null, AlcGetStringList.CaptureDeviceSpecifier);

            return devices;
        }

        private void LogError()
        {
            CheckError(ALCaptureDevice.Null);
        }

        private bool CheckError(ALCaptureDevice device)
        {
            var ALCError = OALCW.GetError(new ALDevice(device));
            if (ALCError == AlcError.NoError) return false;

            Logger.client.VerboseDebug($"[Internal] Failed to open capture device: {ALCError}, {CurrentDevice}, {Frequency}, {SampleFormat}, {BufferSize}");
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

                OALCW.CaptureCloseDevice(_captureDevice);
            }
            IsDisposed = true;
        }
    }
}
