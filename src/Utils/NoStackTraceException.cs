using System;

namespace RPVoiceChat
{
    public class NoStackTraceException : Exception
    {
        public override string StackTrace => null;

        public NoStackTraceException(string message): base(message) { }
    }
}
