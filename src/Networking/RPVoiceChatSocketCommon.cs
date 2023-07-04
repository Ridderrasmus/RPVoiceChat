using Lidgren.Network;
using ProperVersion;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    public class RPVoiceChatSocketCommon
    {
        public ICoreClientAPI capi;
        public ICoreServerAPI sapi;

        public NetPeerConfiguration config = new NetPeerConfiguration("RPVoiceChat");
        public int port = 52525;

        public event EventHandler<NetIncomingMessage> OnMessageReceived;
        public event EventHandler<NetIncomingMessage> OnDiscoveryRequestReceived;
        public event EventHandler<NetIncomingMessage> OnDiscoveryResponseReceived;

        public RPVoiceChatSocketCommon()
        {
        }

        public void StartListening(NetPeer peer)
        {
            Task.Run(() =>
            { 
                // Loop forever
                while (true)
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

                            case NetIncomingMessageType.Data:
                                // Handle incoming data (voice data in our case)
                                OnMessageReceived?.Invoke(this, message);
                                break;

                                // ... handle other types of messages
                        }

                        // Recycle the message to avoid memory leaks
                        peer.Recycle(message);
                    }
                }
            });
        }



    }

}