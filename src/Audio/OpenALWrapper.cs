using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;
using System;

namespace RPVoiceChat.Audio
{
    public static class OALW
    {
        private static ALContext _applicationContext;
        private static ALContext? modContext;
        private static object audio_context_lock = new object();

        public static void InitContext()
        {
            lock (audio_context_lock)
            {
                if (modContext != null) ALC.DestroyContext((ALContext)modContext);

                _applicationContext = ALC.GetCurrentContext();
                var device = ALC.GetContextsDevice(_applicationContext);
                var attrs = new ALContextAttributes();
                modContext = ALC.CreateContext(device, attrs);
            }
        }

        public static void ExecuteInContext(Action action)
        {
            //SetModContext();

            action();

            //ResetContext();
        }

        public static T ExecuteInContext<T>(Func<T> function)
        {
            //SetModContext();

            var result = function();

            //ResetContext();
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

            var currentContext = ALC.GetCurrentContext();
            if (currentContext != modContext)
            {
                Logger.client.Error("Calling CheckError outside of mod context is incorrect");
                return;
            }

            Logger.client.Debug("{0} {1}", Value, AL.GetErrorString(error));
        }

        private static void SetModContext()
        {
            lock (audio_context_lock)
            {
                if (modContext == null) throw new Exception("OpenAL audio context has not been initialized.");
                var currentContext = ALC.GetCurrentContext();
                if (currentContext == modContext) return;
                Logger.client.Debug($"Setting mod context {modContext?.Handle}, current context is {currentContext.Handle}");

                var success = ALC.MakeContextCurrent((ALContext)modContext);
                ALC.ProcessContext((ALContext)modContext);

                if (!success) throw new Exception("Failed to set mod context");
            }
        }

        private static void ResetContext()
        {
            lock (audio_context_lock)
            {
                var currentContext = ALC.GetCurrentContext();
                if (currentContext == _applicationContext) return;
                Logger.client.Debug($"Setting app context {_applicationContext.Handle}, current context is {currentContext.Handle}");

                ALC.SuspendContext((ALContext)modContext);
                var success = ALC.MakeContextCurrent(_applicationContext);

                if (!success) throw new Exception("Failed to set application context");
            }
        }

        public static void Dispose()
        {
            ResetContext();
            ALC.DestroyContext((ALContext)modContext);
            modContext = null;
            _applicationContext = ALContext.Null;
        }
    }
}
