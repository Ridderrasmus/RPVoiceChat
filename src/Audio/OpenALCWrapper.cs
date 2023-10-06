using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace RPVoiceChat.Audio
{
    public static class OALCW
    {
        private static Type _ALDevice;
        private static Type _ALCaptureDevice;

        static OALCW()
        {
            const string assemblyName = "OpenTK.Audio.OpenAL";
            var OpenAL = AppDomain.CurrentDomain.GetAssemblies().ToList().Find(e => e.GetName().Name == assemblyName);
            var ALC = OpenAL.GetType($"{assemblyName}.ALC");
            Type _int = typeof(int);
            Type _string = typeof(string);
            Type _byte = typeof(byte);
            Type _AlcGetInteger = OpenAL.GetType($"{assemblyName}.AlcGetInteger");
            Type _AlcGetString = OpenAL.GetType($"{assemblyName}.AlcGetString");
            Type _AlcGetStringList = OpenAL.GetType($"{assemblyName}.AlcGetStringList");
            Type _ALFormat = OpenAL.GetType($"{assemblyName}.ALFormat");
            _ALCaptureDevice = OpenAL.GetType($"{assemblyName}.ALCaptureDevice");
            _ALDevice = OpenAL.GetType($"{assemblyName}.ALDevice");

            GetInteger_1 = ALC.GetMethod("GetInteger", new Type[] { _ALCaptureDevice, _AlcGetInteger, _int, _int.MakeByRefType() });
            GetString_1 = ALC.GetMethod("GetString", new Type[] { _ALDevice, _AlcGetString });
            GetString_2 = ALC.GetMethod("GetString", new Type[] { _ALDevice, _AlcGetStringList });
            CaptureOpenDevice_1 = ALC.GetMethod("CaptureOpenDevice", new Type[] { _string, _int, _ALFormat, _int });
            CaptureCloseDevice_1 = ALC.GetMethod("CaptureCloseDevice", new Type[] { _ALCaptureDevice });
            CaptureStart_1 = ALC.GetMethod("CaptureStart", new Type[] { _ALCaptureDevice });
            CaptureStop_1 = ALC.GetMethod("CaptureStop", new Type[] { _ALCaptureDevice });
            CaptureSamples_1 = ALC.GetMethods().ToList().FindAll(e => {
                if (e.Name != "CaptureSamples" || e.IsGenericMethod == false) return false;
                if (e.GetGenericArguments().Any(e => e.Name == "T")) return true;
                return false;
            }).Last().MakeGenericMethod(_byte);
            GetError_1 = ALC.GetMethod("GetError", new Type[] { _ALDevice });
        }

        private static MethodInfo GetInteger_1;
        public static void GetInteger(ALCaptureDevice device, AlcGetInteger property, int size, out int value)
        {
            var args = new object[] { Activator.CreateInstance(_ALCaptureDevice, device.Handle), property, size, null };
            GetInteger_1.Invoke(null, args);
            value = (int)args[3];
        }

        private static MethodInfo GetString_1;
        public static string GetString(ALDevice device, AlcGetString property)
        {
            var result = (string)GetString_1.Invoke(null, new object[] { Activator.CreateInstance(_ALDevice, device.Handle), property });

            return result;
        }

        private static MethodInfo GetString_2;
        public static List<string> GetString(ALDevice device, AlcGetStringList property)
        {
            var result = (List<string>)GetString_2.Invoke(null, new object[] { Activator.CreateInstance(_ALDevice, device.Handle), property });

            return result;
        }

        private static MethodInfo CaptureOpenDevice_1;
        public static ALCaptureDevice CaptureOpenDevice(string deviceName, int frequency, ALFormat format, int bufferSize)
        {
            var result = (dynamic)CaptureOpenDevice_1.Invoke(null, new object[] { deviceName, frequency, format, bufferSize });

            return new ALCaptureDevice(result.Handle);
        }

        private static MethodInfo CaptureCloseDevice_1;
        public static void CaptureCloseDevice(ALCaptureDevice device)
        {
            CaptureCloseDevice_1.Invoke(null, new object[] { Activator.CreateInstance(_ALCaptureDevice, device.Handle) });
        }

        private static MethodInfo CaptureStart_1;
        public static void CaptureStart(ALCaptureDevice device)
        {
            CaptureStart_1.Invoke(null, new object[] { Activator.CreateInstance(_ALCaptureDevice, device.Handle) });
        }

        private static MethodInfo CaptureStop_1;
        public static void CaptureStop(ALCaptureDevice device)
        {
            CaptureStop_1.Invoke(null, new object[] { Activator.CreateInstance(_ALCaptureDevice, device.Handle) });
        }

        private static MethodInfo CaptureSamples_1;
        public static void CaptureSamples(ALCaptureDevice device, byte[] buffer, int count)
        {
            CaptureSamples_1.Invoke(null, new object[] { Activator.CreateInstance(_ALCaptureDevice, device.Handle), buffer, count });
        }

        private static MethodInfo GetError_1;
        public static AlcError GetError(ALDevice device)
        {
            var error = (AlcError)GetError_1.Invoke(null, new object[] { Activator.CreateInstance(_ALDevice, device.Handle) });

            return error;
        }

        public static class EFX
        {
            static EFX()
            {
                const string assemblyName = "OpenTK.Audio.OpenAL";
                var OpenAL = AppDomain.CurrentDomain.GetAssemblies().ToList().Find(e => e.GetName().Name == assemblyName);
                var EFX = OpenAL.GetType($"{assemblyName}.ALC").GetNestedType("EFX");
                Type _int = typeof(int);
                Type _float = typeof(float);
                Type _FilterInteger = OpenAL.GetType($"{assemblyName}.FilterInteger");
                Type _FilterFloat = OpenAL.GetType($"{assemblyName}.FilterFloat");

                GenFilter_1 = EFX.GetMethod("GenFilter", new Type[] { });
                Filter_1 = EFX.GetMethod("Filter", new Type[] { _int, _FilterInteger, _int });
                Filter_2 = EFX.GetMethod("Filter", new Type[] { _int, _FilterFloat, _float });
            }

            private static MethodInfo GenFilter_1;
            public static int GenFilter()
            {
                return (int)GenFilter_1.Invoke(null, null);
            }

            private static MethodInfo Filter_1;
            public static void Filter(int filter, FilterInteger param, int value)
            {
                Filter_1.Invoke(null, new object[] { filter, param, value });
            }

            private static MethodInfo Filter_2;
            public static void Filter(int filter, FilterFloat param, float value)
            {
                Filter_2.Invoke(null, new object[] { filter, param, value });
            }
        }
    }

    public enum AlcError : int
    {
        NoError = 0,
        InvalidDevice = 0xA001,
        InvalidContext = 0xA002,
        InvalidEnum = 0xA003,
        InvalidValue = 0xA004,
        OutOfMemory = 0xA005,
    }

    public enum AlcGetString : int
    {
        DefaultDeviceSpecifier = 0x1004,
        Extensions = 0x1006,
        CaptureDefaultDeviceSpecifier = 0x311,
        DefaultAllDevicesSpecifier = 0x1012,
        CaptureDeviceSpecifier = 0x310,
        DeviceSpecifier = 0x1005,
        AllDevicesSpecifier = 0x1013,
    }

    public enum AlcGetStringList : int
    {
        CaptureDeviceSpecifier = 0x310,
        DeviceSpecifier = 0x1005,
        AllDevicesSpecifier = 0x1013,
    }

    public enum AlcGetInteger : int
    {
        MajorVersion = 0x1000,
        MinorVersion = 0x1001,
        AttributesSize = 0x1002,
        AllAttributes = 0x1003,
        CaptureSamples = 0x312,
        EfxMajorVersion = 0x20001,
        EfxMinorVersion = 0x20002,
        EfxMaxAuxiliarySends = 0x20003,
    }

    public struct ALCaptureDevice : IEquatable<ALCaptureDevice>
    {
        public static readonly ALCaptureDevice Null = new ALCaptureDevice(IntPtr.Zero);

        public IntPtr Handle;

        public ALCaptureDevice(IntPtr handle)
        {
            Handle = handle;
        }

        public override bool Equals(object obj)
        {
            return obj is ALCaptureDevice device && Equals(device);
        }

        public bool Equals([AllowNull] ALCaptureDevice other)
        {
            return Handle.Equals(other.Handle);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Handle);
        }

        public static bool operator ==(ALCaptureDevice left, ALCaptureDevice right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ALCaptureDevice left, ALCaptureDevice right)
        {
            return !(left == right);
        }

        public static implicit operator IntPtr(ALCaptureDevice device) => device.Handle;
    }

    public struct ALDevice : IEquatable<ALDevice>
    {
        public static readonly ALDevice Null = new ALDevice(IntPtr.Zero);

        public IntPtr Handle;

        public ALDevice(IntPtr handle)
        {
            Handle = handle;
        }

        public override bool Equals(object obj)
        {
            return obj is ALDevice device && Equals(device);
        }

        public bool Equals([AllowNull] ALDevice other)
        {
            return Handle.Equals(other.Handle);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Handle);
        }

        public static bool operator ==(ALDevice left, ALDevice right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ALDevice left, ALDevice right)
        {
            return !(left == right);
        }

        public static implicit operator IntPtr(ALDevice device) => device.Handle;
    }

    public enum FilterFloat
    {
        LowpassGain = 0x0001,
        LowpassGainHF = 0x0002,
        HighpassGain = 0x0001,
        HighpassGainLF = 0x0002,
        BandpassGain = 0x0001,
        BandpassGainLF = 0x0002,
        BandpassGainHF = 0x0003,
    }

    public enum FilterInteger
    {
        FilterType = 0x8001,
    }

    public enum FilterType
    {
        Null = 0x0000,
        Lowpass = 0x0001,
        Highpass = 0x0002,
        Bandpass = 0x0003,
    }
}
