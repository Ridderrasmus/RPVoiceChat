using Lidgren.Network;
using System;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    public class RPVoiceChatSocketCommon : IDisposable
    {
        public ICoreClientAPI capi;
        public ICoreServerAPI sapi;

        protected NetDeliveryMethod deliveryMethod = NetDeliveryMethod.ReliableSequenced;
        public NetPeerConfiguration config = new NetPeerConfiguration("RPVoiceChat");
        public int port = 52525;


        public event EventHandler<NetIncomingMessage> OnErrorMessageReceived;
        public event EventHandler<NetIncomingMessage> OnStatucChangedReceived;
        public event EventHandler<NetIncomingMessage> OnMessageReceived;
        public event EventHandler<NetIncomingMessage> OnConnectionApprovalMessage;
        public event EventHandler<NetIncomingMessage> OnDiscoveryRequestReceived;
        public event EventHandler<NetIncomingMessage> OnDiscoveryResponseReceived;

        public bool ShouldRun = true;

        public RPVoiceChatSocketCommon()
        {
            config.UseMessageRecycling = true;
        }

        public virtual void Dispose()
        {
            capi = null;
            sapi = null;
            ShouldRun = false;
        }

        public void StartListening(NetPeer peer)
        {
            Task.Run(() =>
            { 
                // Never loop forever, always give yourself an out
                while (ShouldRun)
                {
                    // Read any messages from the server
                    NetIncomingMessage message;
                    while ((message = peer.ReadMessage()) != null)
                    {
                        // Handle different types of messages
                        switch (message.MessageType)
                        {
                            case NetIncomingMessageType.DiscoveryRequest:
                                // Respond to discovery requests
                                peer.SendDiscoveryResponse(null, message.SenderEndPoint);
                                OnDiscoveryRequestReceived?.Invoke(this, message);
                                break;
                            case NetIncomingMessageType.DiscoveryResponse:
                                // Just print the IP of the server
                                OnDiscoveryResponseReceived?.Invoke(this, message);
                                break;
                            case NetIncomingMessageType.ConnectionApproval:
                                // Read the first byte of the packet
                                OnConnectionApprovalMessage?.Invoke(this, message);
                                break;
                            case NetIncomingMessageType.Data:
                                // Handle incoming data (voice data in our case)
                                OnMessageReceived?.Invoke(this, message);
                                break;
                            case NetIncomingMessageType.ErrorMessage:
                                OnErrorMessageReceived?.Invoke(this, message);
                                break;
                            case NetIncomingMessageType.StatusChanged:
                                OnStatucChangedReceived?.Invoke(this, message);
                                break;
                        }

                        // Recycle the message to avoid memory leaks
                        peer.Recycle(message);
                    }
                }
            });
        }

    }

}