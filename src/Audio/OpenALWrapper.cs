using OpenTK;
using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPVoiceChat.Audio
{
    public static class OALW
    {
        private static ContextHandle context;

        public static void InitContext()
        {
            if (context != null) Alc.DestroyContext(context);

            var device = Alc.OpenDevice(null);
            var attrs = new int[0];
            context = Alc.CreateContext(device, attrs);
        }

        public static void Source(int source, ALSourceb property, bool value)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.Source(source, property, value);
            CheckError($"Error setting source {property}");

            ResetContext(ctx);
        }

        public static void Source(int source, ALSourcef property, float value)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.Source(source, property, value);
            CheckError($"Error setting source {property}");

            ResetContext(ctx);
        }

        public static void Source(int source, ALSource3f property, float value1, float value2, float value3)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.Source(source, property, value1, value2, value3);
            CheckError($"Error setting source {property}");

            ResetContext(ctx);
        }

        public static void Listener(ALListenerf property, float value)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.Listener(property, value);
            CheckError($"Error setting listener's {property}");

            ResetContext(ctx);
        }

        public static int GenSource()
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            var source = AL.GenSource();
            CheckError("Error gen source");

            ResetContext(ctx);
            return source;
        }

        public static void GetSource(int source, ALGetSourcei property, out int result)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.GetSource(source, property, out result);

            ResetContext(ctx);
        }

        public static ALSourceState GetSourceState(int source)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            var state = AL.GetSourceState(source);
            CheckError("Error getting source state");

            ResetContext(ctx);
            return state;
        }

        public static void SourcePlay(int source)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.SourcePlay(source);
            CheckError("Error playing source");

            ResetContext(ctx);
        }

        public static void SourceStop(int source)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.SourceStop(source);
            CheckError("Error stop playing source");

            ResetContext(ctx);
        }

        public static void DeleteSource(int source)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.DeleteSource(source);
            CheckError("Error deleting source");

            ResetContext(ctx);
        }

        public static int[] GenBuffers(int bufferCount)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            var buffers = AL.GenBuffers(bufferCount);
            CheckError("Error gen buffers");

            ResetContext(ctx);
            return buffers;
        }

        public static void BufferData(int buffer, ALFormat format, byte[] audio, int length, int frequency)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.BufferData(buffer, format, audio, length, frequency);
            CheckError("Error buffer data");

            ResetContext(ctx);
        }

        public static void SourceQueueBuffer(int source, int buffer)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.SourceQueueBuffer(source, buffer);
            CheckError("Error SourceQueueBuffer");

            ResetContext(ctx);
        }

        public static void SourceUnqueueBuffers(int source, int bufferCount, int[] buffers)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.SourceUnqueueBuffers(source, bufferCount, buffers);
            CheckError("Error SourceUnqueueBuffer", ALError.InvalidValue);

            ResetContext(ctx);
        }

        public static void DeleteBuffers(int[] buffers)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            AL.DeleteBuffers(buffers);

            ResetContext(ctx);
        }

        private static ContextHandle? SetCurrentContext()
        {
            var oldContext = Alc.GetCurrentContext();
            if (oldContext == context) return null;

            Alc.MakeContextCurrent(context);

            return oldContext;
        }

        private static void ResetContext(ContextHandle? ctx)
        {
            if (ctx == null) return;

            Alc.MakeContextCurrent((ContextHandle)ctx);
        }

        private static void CheckError(string Value, ALError ignoredErrors = ALError.NoError)
        {
            var error = AL.GetError();
            if (error == ALError.NoError) return;

            if (ignoredErrors == error)
                return;

            Logger.client.Error("{0} {1}", Value, AL.GetErrorString(error));
        }
    }
}
