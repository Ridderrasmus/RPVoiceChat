using System;

namespace RPVoiceChat.Networking
{
    public class HealthCheckException : Exception
    {
        private const string msg = "failed readiness probe. Aborting to prevent silent malfunction.";
        public override string StackTrace => null;

        public HealthCheckException(NetworkSide side) : base($"{side} {msg}") { }
    }
}
