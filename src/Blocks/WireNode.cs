using ProtoBuf;
using RPVoiceChat.src.Systems;
using RPVoiceChat.Systems;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Blocks
{
    public class WireNode : BlockEntity
    {
        public long NetworkUID { get; set; } = 0;
        public string NodeUID => Pos.ToString();
        private int MaxConnections = 4;

        // Keep local connections for convenience
        private List<WireConnection> Connections = new List<WireConnection>();
        protected EventHandler<string> OnRecievedSignalEvent { get; set; }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                WireNetworkHandler.ClientSideMessageReceived += OnRecievedMessage;
            }
            else
            {
                // Only add if the node isn't already in the network
                if (NetworkUID == 0)
                {
                    NetworkUID = WireNetworkHandler.AddNewNetwork(this).networkID;
                }
                else
                {
                    var net = WireNetworkHandler.GetNetwork(NetworkUID);
                    if (net == null)
                    {
                        // Re-create the network using the same ID
                        net = new WireNetwork { networkID = NetworkUID };
                        WireNetworkHandler.AddNetwork(net);
                    }
                    if (!net.Nodes.Contains(this))
                    {
                        net.AddNode(this);
                    }
                }

                MarkDirty(true);
            }
        }

        public void Connect(WireNode otherNode)
        {
            if (Connections.Count >= MaxConnections) return;

            var network1 = WireNetworkHandler.GetNetwork(this.NetworkUID);
            var network2 = WireNetworkHandler.GetNetwork(otherNode.NetworkUID);
            if (network1 == null || network2 == null) return;

            // Merge if they differ
            WireNetwork finalNetwork;
            if (network1 != network2)
            {
                finalNetwork = WireNetworkHandler.MergeNetworks(new List<WireNetwork> { network1, network2 });
            }
            else
            {
                finalNetwork = network1;
            }

            // Ensure both nodes exist in the final network
            if (!finalNetwork.Nodes.Contains(this))
            {
                finalNetwork.AddNode(this);
            }
            if (!finalNetwork.Nodes.Contains(otherNode))
            {
                finalNetwork.AddNode(otherNode);
            }

            WireConnection newConnection = new WireConnection(this, otherNode);
            finalNetwork.AddConnection(newConnection);

            // Update local connection lists
            Connections.Add(newConnection);
            otherNode.Connections.Add(newConnection);

            // Update network IDs
            this.NetworkUID = finalNetwork.networkID;
            otherNode.NetworkUID = finalNetwork.networkID;

            // Mark both dirty so the changes sync
            this.MarkDirty(true);
            otherNode.MarkDirty(true);
        }

        public void Disconnect(WireNode otherNode)
        {
            var existingConnection = Connections.Find(c => c.InvolvesNode(otherNode));
            if (existingConnection != null)
            {
                Connections.Remove(existingConnection);
                otherNode.Connections.Remove(existingConnection);

                var myNetwork = WireNetworkHandler.GetNetwork(NetworkUID);
                myNetwork?.RemoveConnection(existingConnection);

                // Attempt to see if this breaks the network into sub-networks
                if (myNetwork != null)
                {
                    WireNetworkHandler.AttemptSplitNetwork(myNetwork);
                }
            }
        }

        private void OnRecievedMessage(object sender, WireNetworkMessage e)
        {
            if (e.NetworkUID != NetworkUID || e.SenderPos == Pos) return;
            OnRecievedSignalEvent?.Invoke(this, e.Message);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            WireNetworkHandler.ClientSideMessageReceived -= OnRecievedMessage;

            if (Api.Side == EnumAppSide.Client) return;

            var net = WireNetworkHandler.GetNetwork(NetworkUID);
            net?.RemoveNode(this);
            WireNetworkHandler.AttemptSplitNetwork(net);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            WireNetworkHandler.ClientSideMessageReceived -= OnRecievedMessage;
        }

        public void SendSignal(WireNetworkMessage wireNetworkMessage)
        {
            if (Api.Side == EnumAppSide.Server) return;
            (Api as ICoreClientAPI)?.Network
                .GetChannel(WireNetworkHandler.NetworkChannel)
                .SendPacket(wireNetworkMessage);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            NetworkUID = tree.GetLong("rpvc:networkUID");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetLong("rpvc:networkUID", NetworkUID);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"NetworkUID: {NetworkUID}");
            dsc.AppendLine($"NodeUID: {NodeUID}");

            dsc.AppendLine("Nodes in network:");
            foreach (WireNode node in WireNetworkHandler.GetNetwork(NetworkUID)?.Nodes ?? new List<WireNode>())
            {
                dsc.AppendLine($"Node: {node.NodeUID}");
            }
        }
    }
}
