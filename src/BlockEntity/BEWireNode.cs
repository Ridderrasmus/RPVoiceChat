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

        protected EventHandler<WireNetworkMessage> OnReceivedSignalEvent { get; set; }
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
                if (NetworkUID == 0)
                {
                    // Only create a new network if it is an INetworkRoot
                    if (IsNetworkRoot(this))
                    {
                        WireNetworkHandler.AddNewNetwork(this);
                    }
                }
                else
                {
                    var existing = WireNetworkHandler.GetNetwork(NetworkUID);
                    if (existing != null)
                    {
                        existing.AddNode(this);
                        WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(this, existing);
                    }
                    else if (IsNetworkRoot(this))
                    {
                        // Network was lost, recreate it
                        WireNetworkHandler.AddNewNetwork(this);
                    }
                }
            }

            ResolvePendingConnectionsAndNotify();
        }

        public void MarkForUpdate()
        {
            base.MarkDirty(true);
        }

        /// <summary>
        /// Called when this node creates a new network.
        /// Override in subclasses to track the original network ID.
        /// </summary>
        public virtual void OnNetworkCreated(long networkID)
        {
            // Default implementation does nothing
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

            // Adds the connection on both nodes (AddConnection already checks if connection exists and calls MarkForUpdate)
            connection.Node1.AddConnection(connection);
            connection.Node2.AddConnection(connection);
            OnConnectionsChanged?.Invoke();

            if (Api.Side == EnumAppSide.Server)
            {
                var node1 = connection.Node1 as BEWireNode;
                var node2 = connection.Node2 as BEWireNode;
                if (node1 == null || node2 == null) return;

                var net1 = WireNetworkHandler.GetNetwork(node1.NetworkUID);
                var net2 = WireNetworkHandler.GetNetwork(node2.NetworkUID);

                // Cases where neither has a network AND neither is INetworkRoot -> no network creation
                if (net1 == null && net2 == null && !IsNetworkRoot(node1) && !IsNetworkRoot(node2))
                    return;

                // Manage network creation/merging/propagation
                if (net1 == null && net2 == null)
                {
                    CreateNetworkForComponent(new HashSet<BEWireNode> { node1, node2 });
                }
                else if (net1 != null && net2 == null)
                {
                    net1.AddNode(node2);
                    WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(node2, net1);
                }
                else if (net1 == null && net2 != null)
                {
                    net2.AddNode(node1);
                    WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(node1, net2);
                }
                else if (net1 != null && net2 != null && net1 != net2)
                {
                    // Network merging
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
            if (e.NetworkUID != NetworkUID)
                return;
            // Only skip if we are the sender (avoids duplicate for the player who pressed)
            if (e.SenderPos == Pos && Api.Side == EnumAppSide.Client &&
                (Api as ICoreClientAPI).World.Player?.PlayerUID == e.SenderPlayerUID)
                return;

            OnReceivedSignalEvent?.Invoke(this, e);

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
        /// Checks if a block entity is a network root by checking if it implements INetworkRoot.
        /// </summary>
        private static bool IsNetworkRoot(BEWireNode node)
        {
            return node is INetworkRoot;
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
        /// Prefers a node that implements INetworkRoot, otherwise returns the first node in the component.
        /// </summary>
        private static BEWireNode FindNetworkRoot(IEnumerable<BEWireNode> component)
        {
            if (!component.Any()) return null;
            
            // Prefer INetworkRoot as root
            foreach (var node in component)
            {
                if (IsNetworkRoot(node))
                {
                    return node;
                }
            }
            
            // Otherwise, return the first node
            return component.FirstOrDefault();
        }

        /// <summary>
        /// Creates or reuses a network for a connected component.
        /// Only creates a network if the component contains at least one INetworkRoot.
        /// Otherwise, sets all nodes' NetworkUID to 0.
        /// </summary>
        private static void CreateNetworkForComponent(HashSet<BEWireNode> component)
        {
            if (component.Count == 0) return;
            
            // Check if component contains at least one INetworkRoot
            bool hasNetworkRoot = component.Any(node => IsNetworkRoot(node));
            
            if (!hasNetworkRoot)
            {
                // No INetworkRoot in component, set all NetworkUIDs to 0
                RemoveNodesFromNetwork(component);
                return;
            }
            
            var rootNode = FindNetworkRoot(component);
            if (rootNode == null) return;
            
            // Try to reuse the original NetworkID if available and not used elsewhere
            var network = TryReuseNetworkID(rootNode);
            if (network == null)
            {
                network = WireNetworkHandler.AddNewNetwork(rootNode);
            }
            
            WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(rootNode, network);
        }

        /// <summary>
        /// Tries to reuse the original NetworkID of a root node if it's not used elsewhere.
        /// Also checks if the root is already in a network with its original ID.
        /// Returns the network if successful, null otherwise.
        /// </summary>
        private static WireNetwork TryReuseNetworkID(BEWireNode rootNode)
        {
            if (rootNode is INetworkRoot networkRoot && networkRoot.CreatedNetworkID != 0)
            {
                var existingNetwork = WireNetworkHandler.GetNetwork(networkRoot.CreatedNetworkID);
                
                // If the root is already in a network with its original ID, return that network
                if (existingNetwork != null && existingNetwork.Nodes.Contains(rootNode))
                {
                    return existingNetwork;
                }
                
                // If the original NetworkID is not used, reuse it
                if (existingNetwork == null)
                {
                    var network = new WireNetwork { networkID = networkRoot.CreatedNetworkID };
                    WireNetworkHandler.AddNetwork(network);
                    network.AddNode(rootNode);
                    rootNode.NetworkUID = networkRoot.CreatedNetworkID;
                    rootNode.MarkForUpdate();
                    return network;
                }
            }
            return null;
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
        /// Handles the case where this node is isolated (no connections).
        /// </summary>
        private void HandleIsolatedNode()
        {
            if (IsNetworkRoot(this))
            {
                // If already in a network with the original ID, keep it
                if (this is INetworkRoot networkRoot && networkRoot.CreatedNetworkID != 0)
                {
                    var existingNetwork = WireNetworkHandler.GetNetwork(networkRoot.CreatedNetworkID);
                    if (existingNetwork != null && existingNetwork.Nodes.Contains(this))
                    {
                        // Already in the correct network, nothing to do
                        return;
                    }
                }
                
                // Remove from current network if any
                if (NetworkUID != 0)
                {
                    var oldNetwork = WireNetworkHandler.GetNetwork(NetworkUID);
                    oldNetwork?.RemoveNode(this);
                }
                
                // Try to reuse original ID or create new network
                var network = TryReuseNetworkID(this);
                if (network == null)
                {
                    WireNetworkHandler.AddNewNetwork(this);
                }
            }
            else if (NetworkUID != 0)
            {
                // Not a network root, just remove from network
                var oldNetwork = WireNetworkHandler.GetNetwork(NetworkUID);
                oldNetwork?.RemoveNode(this);
            }
        }

        /// <summary>
        /// Finds the other connected component after a network split.
        /// </summary>
        private static HashSet<BEWireNode> FindOtherComponent(HashSet<BEWireNode> thisComponent, List<BEWireNode> allNetworkNodes)
        {
            var processed = new HashSet<BEWireNode>(thisComponent);
            var filterSet = new HashSet<BEWireNode>(allNetworkNodes);
            
            var otherComponentStart = allNetworkNodes.FirstOrDefault(node => !processed.Contains(node));
            if (otherComponentStart == null) return new HashSet<BEWireNode>();
            
            return FindConnectedComponent(otherComponentStart, filterSet);
        }

        /// <summary>
        /// Finds the original network root that should keep its network.
        /// Returns the root with the oldest CreatedNetworkID (the one that created the network first).
        /// </summary>
        private static BEWireNode FindOriginalNetworkRoot(HashSet<BEWireNode> component1, HashSet<BEWireNode> component2)
        {
            BEWireNode oldestRoot = null;
            long oldestID = long.MaxValue;
            
            foreach (var node in component1.Concat(component2))
            {
                if (node is INetworkRoot networkRoot && networkRoot.CreatedNetworkID != 0)
                {
                    if (networkRoot.CreatedNetworkID < oldestID)
                    {
                        oldestID = networkRoot.CreatedNetworkID;
                        oldestRoot = node;
                    }
                }
            }
            
            return oldestRoot;
        }

        /// <summary>
        /// Removes a component from a network and sets all nodes' NetworkUID to 0.
        /// </summary>
        private static void RemoveComponentFromNetwork(HashSet<BEWireNode> component, WireNetwork network)
        {
            foreach (var node in component)
            {
                node.NetworkUID = 0;
                network.Nodes.Remove(node);
            }
        }

        /// <summary>
        /// Ensures all nodes in a component have the correct NetworkUID.
        /// Only updates nodes that are already in the network (doesn't add new nodes).
        /// </summary>
        private static void EnsureComponentHasNetworkUID(HashSet<BEWireNode> component, WireNetwork network, long networkID)
        {
            foreach (var node in component)
            {
                if (network.Nodes.Contains(node))
                {
                    node.NetworkUID = networkID;
                    node.MarkForUpdate();
                }
            }
        }

        /// <summary>
        /// Handles network splitting when both components have INetworkRoot nodes.
        /// The component with the oldest CreatedNetworkID keeps its network, the other gets a new one or reuses its original.
        /// </summary>
        private static void SplitNetworkWithBothRoots(HashSet<BEWireNode> component, HashSet<BEWireNode> otherComponent, WireNetwork network)
        {
            var originalRoot = FindOriginalNetworkRoot(component, otherComponent);
            if (originalRoot == null) return;
            
            bool originalRootInComponent = component.Contains(originalRoot);
            long originalNetworkID = ((INetworkRoot)originalRoot).CreatedNetworkID;
            
            if (originalRootInComponent)
            {
                RemoveComponentFromNetwork(otherComponent, network);
                EnsureComponentHasNetworkUID(component, network, originalNetworkID);
                CreateNetworkForComponent(otherComponent);
            }
            else
            {
                RemoveComponentFromNetwork(component, network);
                EnsureComponentHasNetworkUID(otherComponent, network, originalNetworkID);
                CreateNetworkForComponent(component);
            }
        }

        /// <summary>
        /// Recalculates networks after a connection is removed.
        /// Only recalculates if:
        /// 1. This node is isolated (no connections) - handles INetworkRoot vs Connector differently
        /// 2. The disconnection split a network - only the disconnected component changes network
        /// </summary>
        private void RecalculateNetworksAfterDisconnection()
        {
            // If this node has no connections, handle isolation
            if (connections.Count == 0)
            {
                HandleIsolatedNode();
                return;
            }

            // If this node still has connections, check if we need to split the network
            if (NetworkUID == 0) return;
            
            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network == null) return;

            var component = FindConnectedComponent(this);
            if (component.Count >= network.Nodes.Count) return; // Network not split

            var allNetworkNodes = new List<BEWireNode>(network.Nodes);
            var otherComponent = FindOtherComponent(component, allNetworkNodes);
            if (otherComponent.Count == 0) return;

            int networkRootsInComponent = component.Count(node => IsNetworkRoot(node));
            int networkRootsInOther = otherComponent.Count(node => IsNetworkRoot(node));

            // Case 1: Both components have INetworkRoot nodes
            if (networkRootsInComponent > 0 && networkRootsInOther > 0)
            {
                SplitNetworkWithBothRoots(component, otherComponent, network);
            }
            // Case 2: Only one component has INetworkRoot nodes
            else if (networkRootsInComponent == 0)
            {
                // This component has no network root, remove it from network
                RemoveNodesFromNetwork(component);
                // The other component keeps the network - ensure it has the correct NetworkUID
                EnsureComponentHasNetworkUID(otherComponent, network, network.networkID);
            }
            else if (networkRootsInOther == 0)
            {
                // Other component has no network root, remove it from network
                RemoveNodesFromNetwork(otherComponent);
                // This component keeps the network - ensure it has the correct NetworkUID
                EnsureComponentHasNetworkUID(component, network, network.networkID);
            }
            // Case 3: Neither component has INetworkRoot nodes - no recalculation needed
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
                if (IsNetworkRoot(this) && Api?.Side == EnumAppSide.Client)
                {
                    dsc.AppendLine(UIUtils.I18n("Network.connecting"));
                }
                else
                {
                    dsc.AppendLine(UIUtils.I18n("Network.NoNetwork"));
                }
                return;
            }

            // On server, verify network exists
            if (Api?.Side == EnumAppSide.Server)
            {
                var network = WireNetworkHandler.GetNetwork(NetworkUID);
                if (network == null)
                {
                    dsc.AppendLine(UIUtils.I18n("Network.NoNetwork"));
                    return;
                }
            }
            
            dsc.AppendLine(UIUtils.I18n("Network.NetworkId", NetworkUID));
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

