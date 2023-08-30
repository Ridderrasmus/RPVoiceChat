using System;
using System.Net;
using System.Threading;

namespace RPVoiceChat
{
    public interface INetworkCommon
    {
        public event Action<byte[]> OnMessageReceived;

        public IPEndPoint GetPublicIP();
    }
}
