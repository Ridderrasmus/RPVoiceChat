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
        public WireNetworkKind CurrentType { get; private set; } = WireNetworkKind.None;
        public bool IsManagedBySwitchboard { get; private set; }
        public bool HasPoweredSwitchboard { get; private set; }
        public int TelegraphEndpointCount { get; private set; }
        public int TelephoneEndpointCount { get; private set; }
        public int WirelessEndpointCount { get; private set; }
        public bool AdvancedTelegraphFeaturesEnabled => IsManagedBySwitchboard && HasPoweredSwitchboard;

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

            RebuildTypedState();
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
            else
            {
                RebuildTypedState();
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
            RebuildTypedState();
        }

        public void RebuildTypedState()
        {
            int telegraph = 0;
            int telephone = 0;
            int wireless = 0;
            bool hasSwitchboard = false;

            foreach (var node in Nodes)
            {
                if (node is IWireTypedNode typedNode)
                {
                    switch (typedNode.WireNodeKind)
                    {
                        case WireNodeKind.Telegraph:
                            telegraph++;
                            break;
                        case WireNodeKind.Telephone:
                            telephone++;
                            break;
                        case WireNodeKind.Wireless:
                            wireless++;
                            break;
                        case WireNodeKind.Switchboard:
                            hasSwitchboard = true;
                            break;
                    }
                }
            }

            TelegraphEndpointCount = telegraph;
            TelephoneEndpointCount = telephone;
            WirelessEndpointCount = wireless;
            IsManagedBySwitchboard = hasSwitchboard;

            int activeKinds = 0;
            if (telegraph > 0) activeKinds++;
            if (telephone > 0) activeKinds++;
            if (wireless > 0) activeKinds++;

            if (activeKinds > 1)
            {
                CurrentType = WireNetworkKind.Mixed;
            }
            else if (telegraph > 0)
            {
                CurrentType = WireNetworkKind.Telegraph;
            }
            else if (telephone > 0)
            {
                CurrentType = WireNetworkKind.Telephone;
            }
            else if (wireless > 0)
            {
                CurrentType = WireNetworkKind.Wireless;
            }
            else
            {
                CurrentType = WireNetworkKind.None;
            }

            HasPoweredSwitchboard = false;
            if (!hasSwitchboard)
            {
                return;
            }

            foreach (var node in Nodes)
            {
                if (node is ISwitchboardNode switchboardNode && switchboardNode.HasSufficientPowerFor(CurrentType))
                {
                    HasPoweredSwitchboard = true;
                    break;
                }
            }
        }
    }
}
