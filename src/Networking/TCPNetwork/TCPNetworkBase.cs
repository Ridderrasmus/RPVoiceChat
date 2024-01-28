using RPVoiceChat.Utils;
using System;
using System.Threading;

namespace RPVoiceChat.Networking
{
    public abstract class TCPNetworkBase : IDisposable
    {
        protected Logger logger;
        protected CancellationTokenSource _readinessProbeCTS;
        protected int port;
        protected const string _transportID = "CustomTCP";
        protected bool isReady = false;

        public TCPNetworkBase(Logger logger)
        {
            this.logger = logger;
        }

        public string GetTransportID()
        {
            return _transportID;
        }

        public virtual void Dispose()
        {
            _readinessProbeCTS?.Cancel();
            _readinessProbeCTS?.Dispose();
        }
    }
}
