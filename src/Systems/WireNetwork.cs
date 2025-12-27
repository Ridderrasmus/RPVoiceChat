using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Systems;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Systems
{
    public class WireNetwork
    {
        public long networkID;
        public List<BEWireNode> Nodes { get; private set; } = new List<BEWireNode>();
        public event Action<BEWireNode, string> OnReceivedSignal;

        public WireNetwork() { }

        public void AddNode(BEWireNode node)
        {
            if (node == null || Nodes.Contains(node))
                return;

            Nodes.Add(node);
            node.NetworkUID = networkID;
            
            // Ensure MarkDirty is called on the main thread to avoid texture upload errors
            if (node.Api?.Side == EnumAppSide.Client)
            {
                ((Vintagestory.API.Client.ICoreClientAPI)node.Api).Event.EnqueueMainThreadTask(() => 
                    node.MarkDirty(true), "MarkDirty");
            }
            else
            {
                node.MarkDirty(true);
            }
        }

        public void RemoveNode(BEWireNode node)
        {
            if (node == null)
                return;

            Nodes.Remove(node);
            node.NetworkUID = 0;
            
            // Ensure MarkDirty is called on the main thread to avoid texture upload errors
            if (node.Api?.Side == EnumAppSide.Client)
            {
                ((Vintagestory.API.Client.ICoreClientAPI)node.Api).Event.EnqueueMainThreadTask(() => 
                    node.MarkDirty(true), "MarkDirty");
            }
            else
            {
                node.MarkDirty(true);
            }

            if (Nodes.Count == 0)
            {
                WireNetworkHandler.RemoveNetwork(this);
            }
        }

        public void SendSignal(BEWireNode sender, string message)
        {
            OnReceivedSignal?.Invoke(sender, message);

            foreach (var node in Nodes)
            {
                if (node != sender)
                {
                    node.SendSignal(new WireNetworkMessage
                    {
                        NetworkUID = networkID,
                        SenderPos = sender.Pos,
                        Message = message
                    });
                }
            }
        }

        public void MergeFrom(WireNetwork otherNetwork)
        {
            if (otherNetwork == null || otherNetwork == this)
                return;

            foreach (var node in otherNetwork.Nodes.ToList())
            {
                AddNode(node);
            }

            WireNetworkHandler.RemoveNetwork(otherNetwork);
        }
    }
}
