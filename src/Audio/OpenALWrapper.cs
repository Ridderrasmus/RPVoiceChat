using OpenTK.Audio.OpenAL;
using RPVoiceChat.Util;

namespace RPVoiceChat.Audio
{
    public static class OALW
    {
        public static void Source(int source, ALSourceb property, bool value)
        {
            AL.Source(source, property, value);
            CheckError($"Error setting source {property}");
        }

        public static void Source(int source, ALSourcef property, float value)
        {
            AL.Source(source, property, value);
            CheckError($"Error setting source {property}");
        }

        public static void Source(int source, ALSource3f property, float value1, float value2, float value3)
        {
            AL.Source(source, property, value1, value2, value3);
            CheckError($"Error setting source {property}");
        }

        public static void Source(int source, ALSourcei property, int value)
        {
            AL.Source(source, property, value);
            CheckError($"Error setting source {property}");
        }

        public static void Listener(ALListenerf property, float value)
        {
            AL.Listener(property, value);
            CheckError($"Error setting listener {property}");
        }

        public static int GenSource()
        {
            var source = AL.GenSource();
            CheckError("Error generating source");

            return source;
        }

        public static void GetSource(int source, ALGetSourcei property, out int value)
        {
            AL.GetSource(source, property, out value);
            CheckError($"Error getting source {property}", ALError.InvalidValue);
        }

        public static ALSourceState GetSourceState(int source)
        {
            GetSource(source, ALGetSourcei.SourceState, out var state);
            // Ignore InvalidValue errors as the source might have been deleted
            CheckError("Error getting source state", ALError.InvalidValue);

            return (ALSourceState)state;
        }

        public static void SourceQueueBuffer(int source, int buffer)
        {
            AL.SourceQueueBuffer(source, buffer);
            CheckError("Error SourceQueueBuffer");
        }

        public static int SourceUnqueueBuffer(int source)
        {
            var buffer = AL.SourceUnqueueBuffer(source);
            CheckError("Error SourceUnqueueBuffer", ALError.InvalidValue);

            return buffer;
        }

        public static void SourcePlay(int source)
        {
            AL.SourcePlay(source);
            CheckError("Error playing source");
        }

        public static void SourceStop(int source)
        {
            AL.SourceStop(source);
            CheckError("Error stop playing source", ALError.InvalidValue);
        }

        public static void DeleteSource(int source)
        {
            try
            {
                AL.DeleteSource(source);
            }
            catch { }
            CheckError("Error deleting source", ALError.InvalidValue);
        }

        public static int[] GenBuffers(int bufferCount)
        {
            var buffers = AL.GenBuffers(bufferCount);
            CheckError("Error gen buffers");

            return buffers;
        }

        public static void BufferData(int currentBuffer, ALFormat format, byte[] audio, int frequency)
        {
            AL.BufferData(currentBuffer, format, audio, frequency);
            CheckError("Error buffer data");
        }

        public static void DeleteBuffers(int[] buffers)
        {
            AL.DeleteBuffers(buffers);
            CheckError("Error deleting buffers");
        }

        public static void ClearError()
        {
            AL.GetError();
        }

        public static void CheckError(string Value, ALError ignoredErrors = ALError.NoError)
        {
            var error = AL.GetError();
            if (error == ALError.NoError) return;
            if (ignoredErrors == error) return;

            Logger.client.Debug("{0} {1}", Value, AL.GetErrorString(error));
        }
    }
}
