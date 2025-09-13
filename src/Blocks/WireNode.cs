using System;
using System.Collections.Generic;
using System.Text;
using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Systems;
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

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

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
            // Ne crée un nouveau réseau QUE si c'est un TelegraphBlock et NetworkUID == 0
            if (NetworkUID == 0)
            {
                if (Block is TelegraphBlock)
                {
                    WireNetworkHandler.AddNewNetwork(this);
                }
                // Sinon networkUID sera assigné plus tard via connexion
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
        MarkDirty(true);
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

        // Ajoute la connexion sur les deux noeuds
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

            // Cas où aucun des deux n'a de réseau ET aucun n'est Telegraph -> pas de création de réseau
            if (!node1HasNetwork && !node2HasNetwork && !node1IsTelegraph && !node2IsTelegraph)
            {
                // Connexion possible, mais pas de création de réseau
                // Les deux NetworkUID restent à 0
                return;
            }

            // Sinon, on doit gérer la création/fusion/propagation réseau

            if (!node1HasNetwork && !node2HasNetwork)
            {
                // Crée un nouveau réseau à partir du Telegraph, ou du node qui est Telegraph sinon n'importe lequel
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
                // Fusion réseaux avec propagation complète
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

        NetworkUID = tree.GetLong("rpvc:networkUID");

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
        if (NetworkUID == 0 || WireNetworkHandler.GetNetwork(NetworkUID) == null)
        {
            dsc.AppendLine("Not connected to any network.");
            return;
        }

        WireNetwork network = WireNetworkHandler.GetNetwork(NetworkUID);
        dsc.AppendLine($"Network ID: {network.networkID}");

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
            dsc.AppendLine("Connections:");
            foreach (WireConnection connection in connections)
            {
                dsc.AppendLine("Connection:");
                dsc.AppendLine($"Node1 - {(connection.Node1?.Position?.ToString() ?? "null")}");
                dsc.AppendLine($"Node2 - {(connection.Node2?.Position?.ToString() ?? "null")}");
                dsc.AppendLine();
            }
        }
        else
        {
            dsc.AppendLine("No connections.");
        }
    }
}
