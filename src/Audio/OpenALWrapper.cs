using RPVoiceChat.Utils;
using System;
using System.Linq;
using System.Reflection;

namespace RPVoiceChat.Audio
{
    public static class OALW
    {
        static OALW()
        {
            const string assemblyName = "OpenTK.Audio.OpenAL";
            var OpenAL = AppDomain.CurrentDomain.GetAssemblies().ToList().Find(e => e.GetName().Name == assemblyName);
            var AL = OpenAL.GetType($"{assemblyName}.AL");
            Type _bool = typeof(bool);
            Type _int = typeof(int);
            Type _intArray = typeof(int[]);
            Type _float = typeof(float);
            Type _string = typeof(string);
            Type _byte = typeof(byte);
            Type _ALSourceb = OpenAL.GetType($"{assemblyName}.ALSourceb");
            Type _ALSourcef = OpenAL.GetType($"{assemblyName}.ALSourcef");
            Type _ALSource3f = OpenAL.GetType($"{assemblyName}.ALSource3f");
            Type _ALSourcei = OpenAL.GetType($"{assemblyName}.ALSourcei");
            Type _ALListenerf = OpenAL.GetType($"{assemblyName}.ALListenerf");
            Type _ALGetSourcei = OpenAL.GetType($"{assemblyName}.ALGetSourcei");
            Type _ALFormat = OpenAL.GetType($"{assemblyName}.ALFormat");
            Type _ALGetString = OpenAL.GetType($"{assemblyName}.ALGetString");
            Type _ALError = OpenAL.GetType($"{assemblyName}.ALError");

            Source_1 = AL.GetMethod("Source", new Type[] { _int, _ALSourceb, _bool });
            Source_2 = AL.GetMethod("Source", new Type[] { _int, _ALSourcef, _float });
            Source_3 = AL.GetMethod("Source", new Type[] { _int, _ALSource3f, _float, _float, _float });
            Source_4 = AL.GetMethod("Source", new Type[] { _int, _ALSourcei, _int });
            Listener_1 = AL.GetMethod("Listener", new Type[] { _ALListenerf, _float });
            GenSource_1 = AL.GetMethod("GenSource", new Type[] { });
            GetSource_1 = AL.GetMethod("GetSource", new Type[] { _int, _ALGetSourcei, _int.MakeByRefType() });
            SourceQueueBuffer_1 = AL.GetMethod("SourceQueueBuffer", new Type[] { _int, _int });
            SourceUnqueueBuffer_1 = AL.GetMethod("SourceUnqueueBuffer", new Type[] { _int });
            SourcePlay_1 = AL.GetMethod("SourcePlay", new Type[] { _int });
            SourceStop_1 = AL.GetMethod("SourceStop", new Type[] { _int });
            DeleteSource_1 = AL.GetMethod("DeleteSource", new Type[] { _int });
            GenBuffers_1 = AL.GetMethod("GenBuffers", new Type[] { _int });
            BufferData_1 = AL.GetMethods().ToList().Find(e => {
                if (e.Name != "BufferData" || e.IsGenericMethod == false) return false;
                if (e.GetGenericArguments().Any(e => e.Name == "TBuffer")) return true;
                return false;
            }).MakeGenericMethod(_byte);
            DeleteBuffers_1 = AL.GetMethod("DeleteBuffers", new Type[] { _intArray });
            Get_1 = AL.GetMethod("Get", new Type[] { _ALGetString });
            GetError_1 = AL.GetMethod("GetError", new Type[] { });
            GetErrorString_1 = AL.GetMethod("GetErrorString", new Type[] { _ALError });
        }

        private static MethodInfo Source_1;
        public static void Source(int source, ALSourceb property, bool value)
        {
            Source_1.Invoke(null, new object[] { source, property, value });
            CheckError($"Error setting source {property}");
        }

        private static MethodInfo Source_2;
        public static void Source(int source, ALSourcef property, float value)
        {
            Source_2.Invoke(null, new object[] { source, property, value });
            CheckError($"Error setting source {property}");
        }

        private static MethodInfo Source_3;
        public static void Source(int source, ALSource3f property, float value1, float value2, float value3)
        {
            Source_3.Invoke(null, new object[] { source, property, value1, value2, value3 });
            CheckError($"Error setting source {property}");
        }

        private static MethodInfo Source_4;
        public static void Source(int source, ALSourcei property, int value)
        {
            Source_4.Invoke(null, new object[] { source, property, value });
            CheckError($"Error setting source {property}");
        }

        private static MethodInfo Listener_1;
        public static void Listener(ALListenerf property, float value)
        {
            Listener_1.Invoke(null, new object[] { property, value });
            CheckError($"Error setting listener {property}");
        }

        private static MethodInfo GenSource_1;
        public static int GenSource()
        {
            var source = (int)GenSource_1.Invoke(null, null);
            CheckError("Error generating source");

            return source;
        }

        private static MethodInfo GetSource_1;
        public static void GetSource(int source, ALGetSourcei property, out int value)
        {
            var args = new object[] { source, property, null };
            GetSource_1.Invoke(null, args);
            value = (int)args[2];
            CheckError($"Error getting source {property}");
        }

        public static ALSourceState GetSourceState(int source)
        {
            GetSource(source, ALGetSourcei.SourceState, out var state);

            return (ALSourceState)state;
        }

        private static MethodInfo SourceQueueBuffer_1;
        public static void SourceQueueBuffer(int source, int buffer)
        {
            SourceQueueBuffer_1.Invoke(null, new object[] { source, buffer });
            CheckError("Error SourceQueueBuffer");
        }

        private static MethodInfo SourceUnqueueBuffer_1;
        public static int SourceUnqueueBuffer(int source)
        {
            var buffer = (int)SourceUnqueueBuffer_1.Invoke(null, new object[] { source });
            CheckError("Error SourceUnqueueBuffer", ALError.InvalidValue);

            return buffer;
        }

        private static MethodInfo SourcePlay_1;
        public static void SourcePlay(int source)
        {
            SourcePlay_1.Invoke(null, new object[] { source });
            CheckError("Error playing source");
        }

        private static MethodInfo SourceStop_1;
        public static void SourceStop(int source)
        {
            SourceStop_1.Invoke(null, new object[] { source });
            CheckError("Error stop playing source");
        }

        private static MethodInfo DeleteSource_1;
        public static void DeleteSource(int source)
        {
            DeleteSource_1.Invoke(null, new object[] { source });
            CheckError("Error deleting source");
        }

        private static MethodInfo GenBuffers_1;
        public static int[] GenBuffers(int bufferCount)
        {
            var buffers = (int[])GenBuffers_1.Invoke(null, new object[] { bufferCount });
            CheckError("Error gen buffers");

            return buffers;
        }

        private static MethodInfo BufferData_1;
        public static void BufferData(int currentBuffer, ALFormat format, byte[] audio, int frequency)
        {
            BufferData_1.Invoke(null, new object[] { currentBuffer, format, audio, frequency });
            CheckError("Error buffer data");
        }

        private static MethodInfo DeleteBuffers_1;
        public static void DeleteBuffers(int[] buffers)
        {
            DeleteBuffers_1.Invoke(null, new object[] { buffers });
            CheckError("Error deleting buffers");
        }

        private static MethodInfo Get_1;
        public static string Get(ALGetString parameter)
        {
            return (string)Get_1.Invoke(null, new object[] { parameter });
        }

        private static MethodInfo GetError_1;
        public static void ClearError()
        {
            GetError_1.Invoke(null, null);
        }

        private static MethodInfo GetErrorString_1;
        public static void CheckError(string Value, ALError ignoredErrors = ALError.NoError)
        {
            var error = (ALError)GetError_1.Invoke(null, null);
            if (error == ALError.NoError) return;
            if (ignoredErrors == error) return;

            Logger.client.Debug("{0} {1}", Value, (string)GetErrorString_1.Invoke(null, new object[] { error }));
        }
    }

    public enum ALListenerf : int
    {
        Gain = 0x100A,
        EfxMetersPerUnit = 0x20004,
    }

    public enum ALSourcef : int
    {
        ReferenceDistance = 0x1020,
        MaxDistance = 0x1023,
        RolloffFactor = 0x1021,
        Pitch = 0x1003,
        Gain = 0x100A,
        MinGain = 0x100D,
        MaxGain = 0x100E,
        ConeInnerAngle = 0x1001,
        ConeOuterAngle = 0x1002,
        ConeOuterGain = 0x1022,
        SecOffset = 0x1024,
        EfxAirAbsorptionFactor = 0x20007,
        EfxRoomRolloffFactor = 0x20008,
        EfxConeOuterGainHighFrequency = 0x20009,
    }

    public enum ALSource3f : int
    {
        Position = 0x1004,
        Velocity = 0x1006,
        Direction = 0x1005,
    }

    public enum ALSourceb : int
    {
        SourceRelative = 0x202,
        Looping = 0x1007,
        EfxDirectFilterGainHighFrequencyAuto = 0x2000A,
        EfxAuxiliarySendFilterGainAuto = 0x2000B,
        EfxAuxiliarySendFilterGainHighFrequencyAuto = 0x2000C,
    }

    public enum ALSourcei : int
    {
        ByteOffset = 0x1026,
        SampleOffset = 0x1025,
        Buffer = 0x1009,
        SourceType = 0x1027,
        EfxDirectFilter = 0x20005,
    }

    public enum ALGetSourcei : int
    {
        ByteOffset = 0x1026,
        SampleOffset = 0x1025,
        Buffer = 0x1009,
        SourceState = 0x1010,
        BuffersQueued = 0x1015,
        BuffersProcessed = 0x1016,
        SourceType = 0x1027,
    }

    public enum ALSourceState : int
    {
        Initial = 0x1011,
        Playing = 0x1012,
        Paused = 0x1013,
        Stopped = 0x1014,
    }

    public enum ALFormat : int
    {
        Mono8 = 0x1100,
        Mono16 = 0x1101,
        Stereo8 = 0x1102,
        Stereo16 = 0x1103,
        MonoALawExt = 0x10016,
        StereoALawExt = 0x10017,
        MonoMuLawExt = 0x10014,
        StereoMuLawExt = 0x10015,
        VorbisExt = 0x10003,
        Mp3Ext = 0x10020,
        MonoIma4Ext = 0x1300,
        StereoIma4Ext = 0x1301,
        MonoFloat32Ext = 0x10010,
        StereoFloat32Ext = 0x10011,
        MonoDoubleExt = 0x10012,
        StereoDoubleExt = 0x10013,
        Multi51Chn16Ext = 0x120B,
        Multi51Chn32Ext = 0x120C,
        Multi51Chn8Ext = 0x120A,
        Multi61Chn16Ext = 0x120E,
        Multi61Chn32Ext = 0x120F,
        Multi61Chn8Ext = 0x120D,
        Multi71Chn16Ext = 0x1211,
        Multi71Chn32Ext = 0x1212,
        Multi71Chn8Ext = 0x1210,
        MultiQuad16Ext = 0x1205,
        MultiQuad32Ext = 0x1206,
        MultiQuad8Ext = 0x1204,
        MultiRear16Ext = 0x1208,
        MultiRear32Ext = 0x1209,
        MultiRear8Ext = 0x1207,
    }

    public enum ALError : int
    {
        NoError = 0,
        InvalidName = 0xA001,
        IllegalEnum = 0xA002,
        InvalidEnum = 0xA002,
        InvalidValue = 0xA003,
        IllegalCommand = 0xA004,
        InvalidOperation = 0xA004,
        OutOfMemory = 0xA005,
    }

    public enum ALGetString : int
    {
        Vendor = 0xB001,
        Version = 0xB002,
        Renderer = 0xB003,
        Extensions = 0xB004,
    }
}
