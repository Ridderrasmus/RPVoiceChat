using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BEWireNode : Vintagestory.API.Common.BlockEntity, IWireConnectable
    {
        public long NetworkUID { get; set; } = 0;
        public BlockPos Position => Pos;
        public string NodeUID => Pos.ToString();
        private int MaxConnections => ServerConfigManager.TelegraphMaxConnectionsPerNode;

        private readonly List<WireConnection> connections = new();
        private List<BlockPos> pendingConnectionPositions = new();
        private IRenderer renderer;

        protected EventHandler<string> OnReceivedSignalEvent { get; set; }
        public event Action OnConnectionsChanged;
        public Vec3f WireAttachmentOffset { get; protected set; } = new Vec3f(0.5f, 0.5f, 0.5f);

        protected virtual void SetWireAttachmentOffset()
        {
            // by default : block center
            WireAttachmentOffset = new Vec3f(0.5f, 0.5f, 0.5f);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            SetWireAttachmentOffset();

            if (api.Side == EnumAppSide.Client)
            {
                var capi = api as ICoreClientAPI;
                var wireRenderer = new WireNodeRenderer(this, capi);
                renderer = wireRenderer;

                WireNetworkHandler.ClientSideMessageReceived += OnReceivedMessage;
            }

            if (Api.Side == EnumAppSide.Server)
            {
                // Only create a new network if it is a NetworkBlock and NetworkUID == 0
                if (NetworkUID == 0)
                {
                    if (IsNetworkBlock(Block))
                    {
                        WireNetworkHandler.AddNewNetwork(this);
                    }
                    // Otherwise, networkUID will be assigned later via connection
                }
                else
                {
                    WireNetwork existing = WireNetworkHandler.GetNetwork(NetworkUID);
                    if (existing != null)
                    {
                        existing.AddNode(this);
                        WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(this, existing);
                    }
                    else
                    {
                        if (IsNetworkBlock(Block))
                        {
                            WireNetworkHandler.AddNewNetwork(this);
                        }
                    }
                }
            }

            ResolvePendingConnectionsAndNotify();
        }

        public void MarkForUpdate()
        {
            base.MarkDirty(true);
        }

        public IReadOnlyList<WireConnection> GetConnections()
        {
            return connections.AsReadOnly();
        }


        private void ResolvePendingConnectionsAndNotify()
        {
            if (pendingConnectionPositions == null || pendingConnectionPositions.Count == 0) return;
            foreach (var otherPos in pendingConnectionPositions)
            {
                var otherBe = Api.World.BlockAccessor.GetBlockEntity(otherPos) as BEWireNode;
                if (otherBe != null)
                {
                    var connection = new WireConnection(this, otherBe);
                    // AddConnection already checks if the connection exists
                    AddConnection(connection);
                    otherBe.AddConnection(connection);
                }
            }
            if (connections.Count > 0)
            {
                OnConnectionsChanged?.Invoke();
            }
            pendingConnectionPositions.Clear();
        }

        public void AddConnection(WireConnection connection)
        {
            if (connection == null) return;

            if (connections.Count >= MaxConnections) return;

            if (!connections.Contains(connection))
            {
                connections.Add(connection);
                MarkForUpdate();
                OnConnectionsChanged?.Invoke();
            }
        }

        public bool HasConnection(WireConnection connection)
        {
            return connections.Contains(connection);
        }

        public void Connect(WireConnection connection)
        {
            if (connection == null || connection.Node1 == null || connection.Node2 == null)
                return;

            if (connections.Count >= MaxConnections || HasConnection(connection))
                return;

            // Adds the connection on both nodes
            // AddConnection already checks if the connection exists, so no need to check HasConnection first
            connection.Node1.AddConnection(connection);
            connection.Node2.AddConnection(connection);

            connection.Node1.MarkForUpdate();
            connection.Node2.MarkForUpdate();

            OnConnectionsChanged?.Invoke();

            if (Api.Side == EnumAppSide.Server)
            {
                BEWireNode node1 = connection.Node1 as BEWireNode;
                BEWireNode node2 = connection.Node2 as BEWireNode;

                WireNetwork net1 = WireNetworkHandler.GetNetwork(node1.NetworkUID);
                WireNetwork net2 = WireNetworkHandler.GetNetwork(node2.NetworkUID);

                bool node1IsNetworkBlock = IsNetworkBlock(node1.Block);
                bool node2IsNetworkBlock = IsNetworkBlock(node2.Block);

                bool node1HasNetwork = net1 != null;
                bool node2HasNetwork = net2 != null;

                // Cases where neither has a network AND neither is NetworkBlock -> no network creation
                if (!node1HasNetwork && !node2HasNetwork && !node1IsNetworkBlock && !node2IsNetworkBlock)
                {
                    // Connection possible, but no network creation
                    // Both NetworkUIDs remain at 0
                    return;
                }

                // Otherwise, we have to manage network creation/merging/propagation
                if (!node1HasNetwork && !node2HasNetwork)
                {
                    // Creates a new network from Telegraph, or from the node that is Telegraph, otherwise any node
                    var component = new HashSet<BEWireNode> { node1, node2 };
                    CreateNetworkForComponent(component);
                }
                else if (node1HasNetwork && !node2HasNetwork)
                {
                    net1.AddNode(node2);
                    WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(node2, net1);
                }
                else if (!node1HasNetwork && node2HasNetwork)
                {
                    net2.AddNode(node1);
                    WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(node1, net2);
                }
                else if (node1HasNetwork && node2HasNetwork && net1 != net2)
                {
                    // Network merging with full propagation
                    if (net1.Nodes.Count >= net2.Nodes.Count)
                    {
                        net1.MergeFrom(net2);
                        WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(node1, net1);
                    }
                    else
                    {
                        net2.MergeFrom(net1);
                        WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(node2, net2);
                    }
                }
            }
        }


        private void OnReceivedMessage(object sender, WireNetworkMessage e)
        {
            if (e.NetworkUID != NetworkUID || e.SenderPos == Pos)
                return;

            OnReceivedSignalEvent?.Invoke(this, e.Message);

            foreach (var conn in connections)
            {
                BEWireNode other = conn.GetOtherNode(this);
                // Check that the other node is not the sender to avoid sending the signal back to the sender
                if (other != null && other.Pos != e.SenderPos)
                {
                    other.SendSignal(new WireNetworkMessage
                    {
                        NetworkUID = this.NetworkUID,
                        SenderPos = this.Pos,
                        Message = e.Message
                    });
                }
            }
        }

        public void SendSignal(WireNetworkMessage wireNetworkMessage)
        {
            if (Api.Side != EnumAppSide.Server)
                return;

            (Api as ICoreServerAPI)?.Network.GetChannel(WireNetworkHandler.NetworkChannel)
                .SendPacket(wireNetworkMessage);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Client)
            {
                // Inform connected nodes to remove their connection client-side as well
                foreach (var connection in new List<WireConnection>(connections))
                {
                    BEWireNode other = connection.GetOtherNode(this);
                    other?.RemoveConnection(connection);
                    other?.MarkForUpdate();
                }

                // Dispose renderer
                if (renderer != null)
                {
                    var capi = Api as ICoreClientAPI;
                    capi?.Event.UnregisterRenderer(renderer, EnumRenderStage.Opaque);
                    if (renderer is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    renderer = null;
                }

                connections.Clear();
                OnConnectionsChanged?.Invoke();

                return;
            }

            foreach (var connection in new List<WireConnection>(connections))
            {
                BEWireNode other = connection.GetOtherNode(this);
                other?.RemoveConnection(connection);
                other?.MarkForUpdate();
            }

            connections.Clear();

            if (NetworkUID != 0)
            {
                var network = WireNetworkHandler.GetNetwork(NetworkUID);
                network?.RemoveNode(this);
            }

            MarkDirty(true);
        }

        public void RemoveConnection(WireConnection connection)
        {
            if (connections.Remove(connection))
            {
                MarkForUpdate();
                OnConnectionsChanged?.Invoke();
                
                // Recalculate networks if needed (only on server side)
                if (Api.Side == EnumAppSide.Server)
                {
                    RecalculateNetworksAfterDisconnection();
                }
            }
        }

        /// <summary>
        /// Checks if a block is a network block (can serve as network root).
        /// Currently only TelegraphBlock, but can be extended in the future.
        /// </summary>
        private static bool IsNetworkBlock(Vintagestory.API.Common.Block block)
        {
            return block is TelegraphBlock;
        }

        /// <summary>
        /// Finds all nodes in the connected component starting from a given node using BFS.
        /// Optionally filters nodes to only include those in the filterNodes set.
        /// </summary>
        private static HashSet<BEWireNode> FindConnectedComponent(BEWireNode startNode, HashSet<BEWireNode> filterNodes = null)
        {
            var component = new HashSet<BEWireNode>();
            var queue = new Queue<BEWireNode>();
            
            queue.Enqueue(startNode);
            component.Add(startNode);
            
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                foreach (var conn in current.GetConnections())
                {
                    var other = conn.GetOtherNode(current);
                    if (other != null && !component.Contains(other))
                    {
                        // If filterNodes is provided, only include nodes that are in the filter
                        if (filterNodes == null || filterNodes.Contains(other))
                        {
                            component.Add(other);
                            queue.Enqueue(other);
                        }
                    }
                }
            }
            
            return component;
        }

        /// <summary>
        /// Finds the root node for a network component.
        /// Prefers a NetworkBlock, otherwise returns the first node in the component.
        /// </summary>
        private static BEWireNode FindNetworkRoot(IEnumerable<BEWireNode> component)
        {
            if (!component.Any()) return null;
            
            // Prefer NetworkBlock as root
            foreach (var node in component)
            {
                if (IsNetworkBlock(node.Block))
                {
                    return node;
                }
            }
            
            // Otherwise, return the first node
            return component.FirstOrDefault();
        }

        /// <summary>
        /// Creates a new network for a connected component.
        /// Only creates a network if the component contains at least one NetworkBlock.
        /// Otherwise, sets all nodes' NetworkUID to 0.
        /// </summary>
        private static void CreateNetworkForComponent(HashSet<BEWireNode> component)
        {
            if (component.Count == 0) return;
            
            // Check if component contains at least one NetworkBlock
            bool hasNetworkBlock = component.Any(node => IsNetworkBlock(node.Block));
            
            if (!hasNetworkBlock)
            {
                // No NetworkBlock in component, set all NetworkUIDs to 0
                foreach (var node in component)
                {
                    if (node.NetworkUID != 0)
                    {
                        var oldNetwork = WireNetworkHandler.GetNetwork(node.NetworkUID);
                        oldNetwork?.RemoveNode(node);
                        node.NetworkUID = 0;
                        node.MarkForUpdate();
                    }
                }
                return;
            }
            
            // Find NetworkBlock as root, or use first node
            var rootNode = FindNetworkRoot(component);
            if (rootNode == null) return;
            
            // Always create a new network for this component
            // (This method is only called for components that need a new network)
            var newNetwork = WireNetworkHandler.AddNewNetwork(rootNode);
            foreach (var node in component)
            {
                if (node != rootNode)
                {
                    newNetwork.AddNode(node);
                }
            }
            WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(rootNode, newNetwork);
        }

        /// <summary>
        /// Removes all nodes in a component from their network and sets their NetworkUID to 0.
        /// </summary>
        private static void RemoveNodesFromNetwork(IEnumerable<BEWireNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.NetworkUID != 0)
                {
                    var oldNetwork = WireNetworkHandler.GetNetwork(node.NetworkUID);
                    oldNetwork?.RemoveNode(node);
                    node.NetworkUID = 0;
                    node.MarkForUpdate();
                }
            }
        }

        /// <summary>
        /// Recalculates networks after a connection is removed.
        /// Only recalculates if:
        /// 1. This node is isolated (no connections) - handles NetworkBlock vs Connector differently
        /// 2. The disconnection split a network between two NetworkBlocks - only the disconnected component changes network
        /// </summary>
        private void RecalculateNetworksAfterDisconnection()
        {
            // If this node has no connections
            if (connections.Count == 0)
            {
                if (NetworkUID != 0)
                {
                    var oldNetwork = WireNetworkHandler.GetNetwork(NetworkUID);
                    oldNetwork?.RemoveNode(this);
                }
                
                // If it's a NetworkBlock, create a new network for it (it's now isolated)
                if (IsNetworkBlock(Block))
                {
                    WireNetworkHandler.AddNewNetwork(this);
                }
                else
                {
                    // Not a NetworkBlock, set NetworkUID to 0
                    if (NetworkUID != 0)
                    {
                        NetworkUID = 0;
                        MarkForUpdate();
                    }
                }
                return;
            }

            // If this node still has connections, check if we need to split the network
            if (NetworkUID == 0) return;
            
            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network == null) return;

            // Find all nodes reachable from this node (connected component)
            var component = FindConnectedComponent(this);

            // Only recalculate if the network was actually split
            if (component.Count >= network.Nodes.Count) return;

            // Count NetworkBlocks in both components
            int networkBlocksInComponent = component.Count(node => IsNetworkBlock(node.Block));
            
            // Find the other component
            var nodesToRemove = new List<BEWireNode>(network.Nodes);
            var processed = new HashSet<BEWireNode>(component);
            var filterSet = new HashSet<BEWireNode>(nodesToRemove);
            
            BEWireNode otherComponentStart = null;
            foreach (var node in nodesToRemove)
            {
                if (!processed.Contains(node))
                {
                    otherComponentStart = node;
                    break;
                }
            }

            if (otherComponentStart == null) return; // Should not happen

            var otherComponent = FindConnectedComponent(otherComponentStart, filterSet);
            int networkBlocksInOther = otherComponent.Count(node => IsNetworkBlock(node.Block));

            // Case 1: Both components have NetworkBlocks - we separated two NetworkBlocks
            // Only one component (the disconnected one) should get a new network
            // The component with the original root keeps its NetworkUID
            if (networkBlocksInComponent > 0 && networkBlocksInOther > 0)
            {
                // Find the original NetworkBlock root before removing nodes
                var originalRoot = FindNetworkRoot(nodesToRemove);
                bool originalRootInThisComponent = component.Contains(originalRoot);
                
                // Save the original network ID before any modifications
                long originalNetworkID = network.networkID;

                if (originalRootInThisComponent)
                {
                    // This component keeps the original network and NetworkUID
                    // First, manually set NetworkUID to 0 for nodes in the other component (before removing from network)
                    foreach (var node in otherComponent)
                    {
                        node.NetworkUID = 0;
                        network.Nodes.Remove(node); // Remove from list without calling RemoveNode (which would set NetworkUID to 0 again)
                    }
                    
                    // Propagate the original NetworkUID to ensure all nodes in this component have it
                    WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(originalRoot, network);
                    
                    // Create new network for the other component (which no longer has the original NetworkUID)
                    CreateNetworkForComponent(otherComponent);
                }
                else
                {
                    // Other component keeps the original network and NetworkUID
                    // First, manually set NetworkUID to 0 for nodes in this component (before removing from network)
                    foreach (var node in component)
                    {
                        node.NetworkUID = 0;
                        network.Nodes.Remove(node); // Remove from list without calling RemoveNode (which would set NetworkUID to 0 again)
                    }
                    
                    // Propagate the original NetworkUID to ensure all nodes in the other component have it
                    WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(originalRoot, network);
                    
                    // Create new network for this component (which no longer has the original NetworkUID)
                    CreateNetworkForComponent(component);
                }
            }
            // Case 2: Only one component has NetworkBlocks - the one without should lose its network
            else if (networkBlocksInComponent == 0)
            {
                // This component has no NetworkBlocks, remove them from network
                RemoveNodesFromNetwork(component);
            }
            else if (networkBlocksInOther == 0)
            {
                // Other component has no NetworkBlocks, remove them from network
                RemoveNodesFromNetwork(otherComponent);
            }
            // Case 3: Neither component has NetworkBlocks - no recalculation needed
            // (They should already have NetworkUID = 0 from CreateNetworkForComponent)
        }

        public override void OnBlockBroken(IPlayer byPlayer)
        {
            base.OnBlockBroken(byPlayer);

            this.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (Api.Side == EnumAppSide.Client)
                WireNetworkHandler.ClientSideMessageReceived -= OnReceivedMessage;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            NetworkUID = tree.GetLong("rpvc:networkUID");

            // Restore network on both client and server side
            if (NetworkUID != 0)
            {
                var existingNetwork = WireNetworkHandler.GetNetwork(NetworkUID);
                if (existingNetwork == null)
                {
                    // Create the network (client-side or server-side)
                    var restoredNetwork = new WireNetwork { networkID = NetworkUID };
                    WireNetworkHandler.AddNetwork(restoredNetwork);
                    restoredNetwork.AddNode(this);
                }
                else
                {
                    // Add this node to the existing network
                    if (!existingNetwork.Nodes.Contains(this))
                    {
                        existingNetwork.AddNode(this);
                    }
                }
            }

            connections.Clear();
            pendingConnectionPositions.Clear();

            if (!tree.HasAttribute("rpvc:connections"))
                return;

            var connArray = tree["rpvc:connections"] as TreeArrayAttribute;
            if (connArray?.value == null || connArray.value.Length == 0)
                return;

            foreach (TreeAttribute connAttr in connArray.value)
            {
                var otherPos = connAttr.GetBlockPos("otherNodePos");
                if (otherPos != null)
                {
                    pendingConnectionPositions.Add(otherPos);
                }
            }

            // Resolve immediately on client to ensure wires render without needing hover
            if (Api?.Side == EnumAppSide.Client)
            {
                ResolvePendingConnectionsAndNotify();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("rpvc:networkUID", NetworkUID);

            List<TreeAttribute> connectionList = new();

            foreach (var conn in connections)
            {
                var other = conn.GetOtherNode(this);
                if (other == null) continue;

                var connAttr = new TreeAttribute();
                connAttr.SetBlockPos("otherNodePos", other.Pos);
                connectionList.Add(connAttr);
            }

            tree["rpvc:connections"] = new TreeArrayAttribute(connectionList.ToArray());
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            // Virtual method for displaying network status (can be overridden)
            DisplayNetworkStatus(forPlayer, dsc);

            // Display connections (common to all)
            DisplayConnections(forPlayer, dsc);
        }

        protected virtual void DisplayNetworkStatus(IPlayer forPlayer, StringBuilder dsc)
        {
            if (NetworkUID == 0)
            {
                if (IsNetworkBlock(Block) && Api?.Side == EnumAppSide.Client)
                {
                    dsc.AppendLine(UIUtils.I18n("Network.connecting"));
                }
                else
                {
                    dsc.AppendLine(UIUtils.I18n("Network.NoNetwork"));
                }
                return;
            }

            // Default display
            if (Api?.Side == EnumAppSide.Client)
            {
                dsc.AppendLine(UIUtils.I18n("Network.NetworkId", NetworkUID));
            }
            else
            {
                WireNetwork network = WireNetworkHandler.GetNetwork(NetworkUID);
                if (network == null)
                {
                    dsc.AppendLine(UIUtils.I18n("Network.NoNetwork"));
                    return;
                }
                dsc.AppendLine(UIUtils.I18n("Network.NetworkId", NetworkUID));
            }
        }

        protected void DisplayConnections(IPlayer forPlayer, StringBuilder dsc)
        {
            if (connections.Count == 0 && pendingConnectionPositions.Count > 0)
            {
                foreach (var otherPos in pendingConnectionPositions)
                {
                    var otherBe = Api.World.BlockAccessor.GetBlockEntity(otherPos) as BEWireNode;
                    if (otherBe != null)
                    {
                        var connection = new WireConnection(this, otherBe);
                        // AddConnection already checks if the connection exists
                        AddConnection(connection);
                    }
                }
            }

            if (connections.Count > 0)
            {
                dsc.AppendLine(UIUtils.I18n("Network.Connections"));
                foreach (WireConnection connection in connections)
                {
                    dsc.AppendLine(UIUtils.I18n("Network.ConnectionBetween", connection.Node1?.Position?.ToString(), connection.Node2?.Position?.ToString()));
                }
                dsc.AppendLine();
            }
            else
            {
                dsc.AppendLine(UIUtils.I18n("Network.NoConnections"));
            }
        }
    }
}

