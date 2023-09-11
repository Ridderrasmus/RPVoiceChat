using OpenTK;
using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;
using System;

namespace RPVoiceChat.Audio
{
    public static class OALW
    {
        private static ContextHandle context;
        private static object audio_context_lock = new object();

        public static void InitContext()
        {
            if (context != null) Alc.DestroyContext(context);

            var device = Alc.OpenDevice(null);
            var attrs = new int[0];
            context = Alc.CreateContext(device, attrs);
        }

        public static void ExecuteInContext(Action action)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            action();

            ResetContext(ctx);
        }

        public static T ExecuteInContext<T>(Func<T> function)
        {
            if (context == null) throw new Exception("OpenAL audio context has not been initialized.");
            var ctx = SetCurrentContext();

            var result = function();

            ResetContext(ctx);
            return result;
        }

        public static void Source(int source, ALSourceb property, bool value)
        {
            ExecuteInContext(() =>
            {
                AL.Source(source, property, value);
                CheckError($"Error setting source {property}");
            });
        }

        public static void Source(int source, ALSourcef property, float value)
        {
            ExecuteInContext(() =>
            {
                AL.Source(source, property, value);
                CheckError($"Error setting source {property}");
            });
        }

        public static void Listener(ALListenerf property, float value)
        {
            ExecuteInContext(() =>
            {
                AL.Listener(property, value);
                CheckError($"Error setting listener's {property}");
            });
        }

        public static int GenSource()
        {
            return ExecuteInContext(() =>
            {
                var source = AL.GenSource();
                CheckError("Error generating source");

                return source;
            });
        }

        public static ALSourceState GetSourceState(int source)
        {
            return ExecuteInContext(() =>
            {
                var state = AL.GetSourceState(source);
                CheckError("Error getting source state");

                return state;
            });
        }

        public static void SourcePlay(int source)
        {
            ExecuteInContext(() =>
            {
                AL.SourcePlay(source);
                CheckError("Error playing source");
            });
        }

        public static void SourceStop(int source)
        {
            ExecuteInContext(() =>
            {
                AL.SourceStop(source);
                CheckError("Error stop playing source");
            });
        }

        public static void DeleteSource(int source)
        {
            ExecuteInContext(() =>
            {
                AL.DeleteSource(source);
                CheckError("Error deleting source");
            });
        }

        public static int[] GenBuffers(int bufferCount)
        {
            return ExecuteInContext(() =>
            {
                var buffers = AL.GenBuffers(bufferCount);
                CheckError("Error gen buffers");

                return buffers;
            });
        }

        public static void DeleteBuffers(int[] buffers)
        {
            ExecuteInContext(() =>
            {
                AL.DeleteBuffers(buffers);
                CheckError("Error deleting buffers");
            });
        }

        public static void CheckError(string Value, ALError ignoredErrors = ALError.NoError)
        {
            var error = AL.GetError();
            if (error == ALError.NoError) return;
            if (ignoredErrors == error) return;

            Logger.client.Error("{0} {1}", Value, AL.GetErrorString(error));
        }

        private static ContextHandle? SetCurrentContext()
        {
            lock (audio_context_lock)
            {
                var oldContext = Alc.GetCurrentContext();
                if (oldContext == context) return null;

                Alc.MakeContextCurrent(context);

                return oldContext;
            }
        }

        private static void ResetContext(ContextHandle? ctx)
        {
            lock (audio_context_lock)
            {
                if (ctx == null) return;

                Alc.MakeContextCurrent((ContextHandle)ctx);
            }
        }
    }
}
