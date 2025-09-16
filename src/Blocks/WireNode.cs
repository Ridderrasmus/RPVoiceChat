using System;
using System.Collections.Generic;
using System.Text;
using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Systems;
using RPVoiceChat.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

public class WireNode : BlockEntity, IWireConnectable
{
    public long NetworkUID { get; set; } = 0;
    public BlockPos Position => Pos;
    public string NodeUID => Pos.ToString();
    private const int MaxConnections = 4;

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
            capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "wirenode");

            this.OnConnectionsChanged += () =>
            {
                wireRenderer.MarkNeedsRebuild();
            };

            WireNetworkHandler.ClientSideMessageReceived += OnReceivedMessage;
        }

        if (Api.Side == EnumAppSide.Server)
        {
            // Only create a new network if it is a TelegraphBlock and NetworkUID == 0
            if (NetworkUID == 0)
            {
                if (Block is TelegraphBlock)
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
                    if (Block is TelegraphBlock)
                    {
                        WireNetworkHandler.AddNewNetwork(this);
                    }
                }
            }
        }

        foreach (var otherPos in pendingConnectionPositions)
        {
            var otherBe = api.World.BlockAccessor.GetBlockEntity(otherPos) as WireNode;
            if (otherBe != null)
            {
                var connection = new WireConnection(this, otherBe);
                if (!HasConnection(connection)) AddConnection(connection);
                if (!otherBe.HasConnection(connection)) otherBe.AddConnection(connection);
            }
        }

        if (connections.Count > 0)
        {
            OnConnectionsChanged?.Invoke();
        }

        pendingConnectionPositions.Clear();
    }

    public void MarkForUpdate()
    {
        base.MarkDirty(true);
    }

    public IReadOnlyList<WireConnection> GetConnections()
    {
        return connections.AsReadOnly();
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
        if (!connection.Node1.HasConnection(connection))
            connection.Node1.AddConnection(connection);

        if (!connection.Node2.HasConnection(connection))
            connection.Node2.AddConnection(connection);

        connection.Node1.MarkForUpdate();
        connection.Node2.MarkForUpdate();

        OnConnectionsChanged?.Invoke();

        if (Api.Side == EnumAppSide.Server)
        {
            WireNode node1 = connection.Node1 as WireNode;
            WireNode node2 = connection.Node2 as WireNode;

            WireNetwork net1 = WireNetworkHandler.GetNetwork(node1.NetworkUID);
            WireNetwork net2 = WireNetworkHandler.GetNetwork(node2.NetworkUID);

            bool node1IsTelegraph = node1.Block is TelegraphBlock;
            bool node2IsTelegraph = node2.Block is TelegraphBlock;

            bool node1HasNetwork = net1 != null;
            bool node2HasNetwork = net2 != null;

            // Cases where neither has a network AND neither is Telegraph -> no network creation
            if (!node1HasNetwork && !node2HasNetwork && !node1IsTelegraph && !node2IsTelegraph)
            {
                // Connection possible, but no network creation
                // Both NetworkUIDs remain at 0
                return;
            }

            // Otherwise, we have to manage network creation/merging/propagation
            if (!node1HasNetwork && !node2HasNetwork)
            {
                // Creates a new network from Telegraph, or from the node that is Telegraph, otherwise any node
                WireNode networkRoot = node1IsTelegraph ? node1 : node2IsTelegraph ? node2 : node1;
                var newNet = WireNetworkHandler.AddNewNetwork(networkRoot);
                newNet.AddNode(node1IsTelegraph ? node2 : node1);
                WireNetworkHandler.PropagateNetworkUIDToConnectedNodes(networkRoot, newNet);
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
            WireNode other = conn.GetOtherNode(this);
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
            WireNode other = connection.GetOtherNode(this);
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
        }
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

        long oldNetworkUID = NetworkUID;
        NetworkUID = tree.GetLong("rpvc:networkUID");

        // On the client side, create the network if it does not exist
        if (Api?.Side == EnumAppSide.Client && NetworkUID != 0)
        {
            var existingNetwork = WireNetworkHandler.GetNetwork(NetworkUID);
            if (existingNetwork == null)
            {
                // Create the client-side network
                var clientNetwork = new WireNetwork { networkID = NetworkUID };
                WireNetworkHandler.AddNetwork(clientNetwork);
                clientNetwork.AddNode(this);
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
            if (Block is TelegraphBlock && Api?.Side == EnumAppSide.Client)
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
                var otherBe = Api.World.BlockAccessor.GetBlockEntity(otherPos) as WireNode;
                if (otherBe != null)
                {
                    var connection = new WireConnection(this, otherBe);
                    if (!HasConnection(connection)) AddConnection(connection);
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
