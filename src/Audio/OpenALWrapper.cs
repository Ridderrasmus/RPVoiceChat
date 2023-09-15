using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;
using System;

namespace RPVoiceChat.Audio
{
    public static class OALW
    {
        private static ALContext context;
        private static ALContext _activeContext;
        private static object audio_context_lock = new object();

        public static void InitContext()
        {
            lock (audio_context_lock)
            {
                if (context != null) ALC.DestroyContext(context);

                var device = ALC.OpenDevice(null);
                var attrs = new int[0];
                context = ALC.CreateContext(device, attrs);
            }
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
                AL.GetSource(source, ALGetSourcei.SourceState, out var state);
                CheckError("Error getting source state");

                return (ALSourceState)state;
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

            if (_activeContext != context)
            {
                Logger.client.Error("Calling CheckError outside of mod context is incorrect");
                return;
            }

            Logger.client.Debug("{0} {1}", Value, AL.GetErrorString(error));
        }

        private static ALContext? SetCurrentContext()
        {
            lock (audio_context_lock)
            {
                var oldContext = ALC.GetCurrentContext();
                if (oldContext == context) return null;

                ALC.MakeContextCurrent(context);
                _activeContext = context;

                return oldContext;
            }
        }

        private static void ResetContext(ALContext? ctx)
        {
            lock (audio_context_lock)
            {
                if (ctx == null) return;

                ALC.MakeContextCurrent((ALContext)ctx);
                _activeContext = (ALContext)ctx;
            }
        }
    }
}
