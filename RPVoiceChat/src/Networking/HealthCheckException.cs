namespace RPVoiceChat.Networking
{
    public class HealthCheckException : NoStackTraceException
    {
        private const string msg = "failed readiness probe. Aborting to prevent silent malfunction.";

        public HealthCheckException(NetworkSide side) : base($"{side} {msg}") { }
    }
}
