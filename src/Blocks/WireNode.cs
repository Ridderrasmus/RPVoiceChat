using System;
using System.Collections.Generic;
using System.Text;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.GameContent.Blocks
{
    public class WireNode : BlockEntity
    {
        public long NetworkUID { get; set; } = 0;
        public string NodeUID => Pos.ToString();
        private const int MaxConnections = 4;
        private readonly List<WireConnection> Connections = new();

        protected EventHandler<string> OnReceivedSignalEvent { get; set; }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                WireNetworkHandler.ClientSideMessageReceived += OnReceivedMessage;
            }
            else
            {
                if (NetworkUID != 0)
                {
                    var network = WireNetworkHandler.GetNetwork(NetworkUID);
                    if (network == null)
                    {
                        network = new WireNetwork { networkID = NetworkUID };
                        WireNetworkHandler.AddNetwork(network);
                    }

                    network.AddNode(this);
                }
                else
                {
                    WireNode neighbor = FindConnectedNeighbor();
                    if (neighbor != null && neighbor.NetworkUID != 0)
                    {
                        NetworkUID = neighbor.NetworkUID;
                    }
                    else
                    {
                        var newNetwork = WireNetworkHandler.AddNewNetwork(this);
                        NetworkUID = newNetwork.networkID;
                    }

                    var network = WireNetworkHandler.GetNetwork(NetworkUID);
                    network?.AddNode(this);
                }

                MarkDirty(true);
            }
        }

        public void Connect(WireConnection connection)
        {
            if (Connections.Count >= MaxConnections)
            {
                return;
            }
            if (Connections.Contains(connection))
            {
                return;
            }
            if (connection.Node1 == null || connection.Node2 == null)
            {
                return;
            }

            if (!connection.Node1.Connections.Contains(connection))
                connection.Node1.Connections.Add(connection);

            if (!connection.Node2.Connections.Contains(connection))
                connection.Node2.Connections.Add(connection);

            connection.Node1.MarkDirty(true);
            connection.Node2.MarkDirty(true);

            if (Api.Side == EnumAppSide.Server)
            {
                WireNode node1 = connection.Node1;
                WireNode node2 = connection.Node2;

                if (node1.NetworkUID != node2.NetworkUID)
                {
                    var net1 = WireNetworkHandler.GetNetwork(node1.NetworkUID);
                    var net2 = WireNetworkHandler.GetNetwork(node2.NetworkUID);

                    if (net1 != null && net2 != null)
                    {
                        if (net1.Nodes.Count >= net2.Nodes.Count)
                        {
                            net1.MergeFrom(net2);
                        }
                        else
                        {
                            net2.MergeFrom(net1);
                        }
                    }
                }
            }
        }

        private void OnReceivedMessage(object sender, WireNetworkMessage e)
        {
            if (e.NetworkUID != NetworkUID || e.SenderPos == Pos)
                return;

            OnReceivedSignalEvent?.Invoke(this, e.Message);

            foreach (var conn in Connections)
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

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Client)
                return;

            foreach (var connection in new List<WireConnection>(Connections))
            {
                WireNode other = connection.GetOtherNode(this);
                other?.Connections.Remove(connection);
            }
            Connections.Clear();

            if (NetworkUID != 0)
            {
                var network = WireNetworkHandler.GetNetwork(NetworkUID);
                network?.RemoveNode(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            WireNetworkHandler.ClientSideMessageReceived -= OnReceivedMessage;
        }

        public void SendSignal(WireNetworkMessage wireNetworkMessage)
        {
            if (Api.Side != EnumAppSide.Server)
                return;

            (Api as ICoreServerAPI)?.Network.GetChannel(WireNetworkHandler.NetworkChannel).SendPacket(wireNetworkMessage);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            NetworkUID = tree.GetLong("rpvc:networkUID");
            Connections.Clear();

            if (!tree.HasAttribute("rpvc:connections"))
                return;

            var connArray = tree["rpvc:connections"] as TreeArrayAttribute;
            if (connArray?.value == null || connArray.value.Length == 0)
                return;

            foreach (TreeAttribute connAttr in connArray.value)
            {
                var otherPos = connAttr.GetBlockPos("otherNodePos");
                if (otherPos == null) continue;

                if (worldAccessForResolve.BlockAccessor.GetBlockEntity(otherPos) is WireNode otherNode)
                {
                    var connection = new WireConnection(this, otherNode);
                    if (!Connections.Contains(connection))
                    {
                        Connections.Add(connection);
                    }
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("rpvc:networkUID", NetworkUID);

            List<TreeAttribute> connectionList = new();

            foreach (var conn in Connections)
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
            dsc.AppendLine($"[DEBUG] Client sees {Connections.Count} connections.");

            if (Connections != null && Connections.Count > 0)
            {
                dsc.AppendLine("Connections:");
                foreach (WireConnection connection in Connections)
                {
                    dsc.AppendLine("Connection:");
                    dsc.AppendLine($"Node1 - {(connection.Node1?.Pos?.ToString() ?? "null")}");
                    dsc.AppendLine($"Node2 - {(connection.Node2?.Pos?.ToString() ?? "null")}");
                    dsc.AppendLine();
                }
            }
            else
            {
                dsc.AppendLine("No connections.");
            }
        }

        private WireNode FindConnectedNeighbor()
        {
            foreach (BlockFacing face in BlockFacing.HORIZONTALS)
            {
                BlockPos neighborPos = Pos.AddCopy(face);
                var be = Api.World.BlockAccessor.GetBlockEntity(neighborPos) as WireNode;
                if (be != null && be.NetworkUID != 0)
                {
                    return be;
                }
            }

            return null;
        }
    }
}
