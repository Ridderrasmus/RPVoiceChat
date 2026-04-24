using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Systems;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Systems
{
    public class WireNetwork : CommunicationNetworkBase
    {
        public override NetworkTransportType TransportType => NetworkTransportType.Wired;
        public List<BEWireNode> Nodes { get; private set; } = new List<BEWireNode>();
        public event Action<BEWireNode, string> OnReceivedSignal;
        public WireNetworkKind CurrentType { get; private set; } = WireNetworkKind.None;
        public bool IsManagedBySwitchboard { get; private set; }
        public bool HasPoweredSwitchboard { get; private set; }
        public int TelegraphEndpointCount { get; private set; }
        public int TelephoneEndpointCount { get; private set; }
        public int RadioEndpointCount { get; private set; }
        public bool AdvancedTelegraphFeaturesEnabled => IsManagedBySwitchboard && HasPoweredSwitchboard;

        public WireNetwork() { }

        public void AddNode(BEWireNode node)
        {
            if (node == null || Nodes.Contains(node))
                return;

            Nodes.Add(node);
            node.NetworkUID = networkID;

            // Rebuild routing snapshots before syncing BE data so rpvc:routing* and related flags are not one tick stale.
            RebuildTypedState();

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

            if (node.Api?.Side == EnumAppSide.Server && node is BlockEntityTelegraph detachedTelegraph)
            {
                detachedTelegraph.ApplyServerRoutingFlags(false, false, "Telegraph.Settings.DisabledNoPower");
            }
            
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
            int radio = 0;
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
                        case WireNodeKind.Radio:
                            radio++;
                            break;
                        case WireNodeKind.Switchboard:
                            hasSwitchboard = true;
                            break;
                    }
                }
            }

            TelegraphEndpointCount = telegraph;
            TelephoneEndpointCount = telephone;
            RadioEndpointCount = radio;
            IsManagedBySwitchboard = hasSwitchboard;

            int activeKinds = 0;
            if (telegraph > 0) activeKinds++;
            if (telephone > 0) activeKinds++;
            if (radio > 0) activeKinds++;

            if (activeKinds > 1)
            {
                // Guard state: mixed networks are forbidden by connection rules.
                CurrentType = WireNetworkKind.None;
            }
            else if (telegraph > 0)
            {
                CurrentType = WireNetworkKind.Telegraph;
            }
            else if (telephone > 0)
            {
                CurrentType = WireNetworkKind.Telephone;
            }
            else if (radio > 0)
            {
                CurrentType = WireNetworkKind.Radio;
            }
            else
            {
                CurrentType = WireNetworkKind.None;
            }

            HasPoweredSwitchboard = false;
            if (hasSwitchboard)
            {
                foreach (var node in Nodes)
                {
                    if (node is ISwitchboardNode switchboardNode && switchboardNode.HasSufficientPowerFor(CurrentType))
                    {
                        HasPoweredSwitchboard = true;
                        break;
                    }
                }
            }

            // Must run even when there is no switchboard (e.g. reset flags after removal, or first telegraph-only net).
            WireNetworkHandler.RefreshTelegraphRoutingSnapshot(networkID);
        }

    }
}
