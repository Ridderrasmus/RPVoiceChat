using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Systems
{
    public class WireNetwork : CommunicationNetworkBase
    {
        public override NetworkTransportType TransportType => NetworkTransportType.Wired;

        /// <summary>Loaded block entities currently attached to this network (runtime only).</summary>
        public List<BEWireNode> Nodes { get; private set; } = new List<BEWireNode>();

        /// <summary>Authoritative node list persisted at world level; kept when chunks unload.</summary>
        public List<WireNodeRef> PersistedNodes { get; private set; } = new List<WireNodeRef>();

        public event Action<BEWireNode, string> OnReceivedSignal;
        public WireNetworkKind CurrentType { get; private set; } = WireNetworkKind.None;
        public bool IsManagedBySwitchboard { get; private set; }
        public bool HasPoweredSwitchboard { get; private set; }
        public int TelegraphEndpointCount { get; private set; }
        public int TelephoneEndpointCount { get; private set; }
        public int RadioEndpointCount { get; private set; }
        public bool AdvancedTelegraphFeaturesEnabled => IsManagedBySwitchboard && HasPoweredSwitchboard;
        public bool HasPersistedNodes => PersistedNodes.Count > 0;

        public WireNetwork() { }

        public static WireNetwork FromSaveData(WireNetworkSaveData data)
        {
            var network = new WireNetwork { networkID = data.NetworkId };
            network.SetCustomName(data.CustomName ?? "");
            network.PersistedNodes = data.Nodes != null
                ? new List<WireNodeRef>(data.Nodes)
                : new List<WireNodeRef>();
            return network;
        }

        public WireNetworkSaveData ToSaveData()
        {
            return new WireNetworkSaveData
            {
                NetworkId = networkID,
                CustomName = CustomName,
                Nodes = new List<WireNodeRef>(PersistedNodes)
            };
        }

        public void AddNode(BEWireNode node)
        {
            JoinNode(node);
        }

        /// <summary>Attach a loaded block entity to this network (chunk load / placement).</summary>
        public void JoinNode(BEWireNode node)
        {
            if (node == null)
                return;

            UpsertPersistedNode(node);

            if (Nodes.Contains(node))
            {
                if (node.NetworkUID != networkID)
                {
                    node.NetworkUID = networkID;
                    MarkNodeDirty(node);
                }
                return;
            }

            Nodes.Add(node);
            node.NetworkUID = networkID;
            RebuildTypedState();
            MarkNodeDirty(node);
        }

        /// <summary>Detach a loaded block entity when its chunk unloads. Keeps <see cref="PersistedNodes"/>.</summary>
        public void DetachNode(BEWireNode node)
        {
            if (node == null || !Nodes.Remove(node))
                return;

            if (node.Api?.Side == EnumAppSide.Server && node is BlockEntityTelegraph detachedTelegraph)
            {
                detachedTelegraph.ApplyServerRoutingFlags(false, false, "Telegraph.Settings.DisabledNoPower");
            }

            MarkNodeDirty(node);

            if (Nodes.Count > 0)
            {
                RebuildTypedState();
            }
        }

        public void RemoveNode(BEWireNode node)
        {
            if (node == null)
                return;

            Nodes.Remove(node);
            RemovePersistedNode(node.Pos);
            node.NetworkUID = 0;

            if (node.Api?.Side == EnumAppSide.Server && node is BlockEntityTelegraph detachedTelegraph)
            {
                detachedTelegraph.ApplyServerRoutingFlags(false, false, "Telegraph.Settings.DisabledNoPower");
            }

            MarkNodeDirty(node);

            if (Nodes.Count == 0 && PersistedNodes.Count == 0)
            {
                WireNetworkHandler.RemoveNetwork(this);
            }
            else if (Nodes.Count > 0)
            {
                RebuildTypedState();
            }
        }

        public void UpsertPersistedNode(BEWireNode node)
        {
            if (node?.Pos == null)
                return;

            var nodeRef = CreateNodeRef(node);
            int index = PersistedNodes.FindIndex(existing => existing.Pos != null && existing.Pos.Equals(nodeRef.Pos));
            if (index >= 0)
            {
                PersistedNodes[index] = nodeRef;
            }
            else
            {
                PersistedNodes.Add(nodeRef);
            }
        }

        public void RemovePersistedNode(BlockPos pos)
        {
            if (pos == null)
                return;

            PersistedNodes.RemoveAll(existing => existing.Pos != null && existing.Pos.Equals(pos));
        }

        public void UpsertPersistedNodeFromPosition(BlockPos pos, IBlockAccessor accessor)
        {
            if (pos == null)
            {
                return;
            }

            var node = accessor?.GetBlockEntity(pos) as BEWireNode;
            if (node != null)
            {
                UpsertPersistedNode(node);
                return;
            }

            int index = PersistedNodes.FindIndex(existing => existing.Pos != null && existing.Pos.Equals(pos));
            if (index < 0)
            {
                PersistedNodes.Add(new WireNodeRef(pos.Copy(), WireNodeKind.Infrastructure));
            }
        }

        public void MergePersistedNodesFrom(WireNetwork otherNetwork)
        {
            if (otherNetwork == null)
                return;

            foreach (var nodeRef in otherNetwork.PersistedNodes)
            {
                if (nodeRef?.Pos == null)
                    continue;

                int index = PersistedNodes.FindIndex(existing => existing.Pos != null && existing.Pos.Equals(nodeRef.Pos));
                if (index >= 0)
                {
                    PersistedNodes[index] = nodeRef;
                }
                else
                {
                    PersistedNodes.Add(nodeRef);
                }
            }
        }

        private static WireNodeRef CreateNodeRef(BEWireNode node)
        {
            WireNodeKind kind = WireNodeKind.Infrastructure;
            if (node is IWireTypedNode typedNode)
            {
                kind = typedNode.WireNodeKind;
            }

            return new WireNodeRef(node.Pos.Copy(), kind);
        }

        private static void MarkNodeDirty(BEWireNode node)
        {
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

            MergePersistedNodesFrom(otherNetwork);

            foreach (var node in otherNetwork.Nodes.ToList())
            {
                JoinNode(node);
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
                        case WireNodeKind.RadioConsole:
                        case WireNodeKind.RadioEmitter:
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
