using ProtoBuf;
using RPVoiceChat.src.Systems;
using RPVoiceChat.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
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
                // For now we simply handle one network at all here but in future we want to check if the block as the current position is already in a network
                // If it is we want to add it to that network instead of creating a new one
                // Otherwise we create a new network
                WireNetworkHandler.GetNetwork(NetworkUID).AddNode(this);
            }

        }

        public void Connect(WireConnection connection)
        {
            if (Connections.Count >= MaxConnections)
                return;

            if (Connections.Contains(connection))
                return;

            if (connection.Node1 == null || connection.Node2 == null)
                return;

            connection.Node1.Connections.Add(connection);
            connection.Node2.Connections.Add(connection);
        }

        private void OnRecievedMessage(object sender, WireNetworkMessage e)
        {

            if (e.NetworkUID != NetworkUID || e.SenderPos == Pos)
                return;

            OnRecievedSignalEvent?.Invoke(this, e.Message);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Client)
                return;

            WireNetworkHandler.GetNetwork(NetworkUID).RemoveNode(this);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            WireNetworkHandler.ClientSideMessageReceived -= OnRecievedMessage;
        }

        public void SendSignal(WireNetworkMessage wireNetworkMessage)
        {
            if (Api.Side == EnumAppSide.Server)
                return;

            (Api as ICoreClientAPI).Network.GetChannel(WireNetworkHandler.NetworkChannel).SendPacket(wireNetworkMessage);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            NetworkUID = tree.GetInt("rpvc:networkUID");

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
            foreach (WireNode node in WireNetworkHandler.GetNetwork(NetworkUID).Nodes)
            {
                dsc.AppendLine($"Node: {node.NodeUID}");
            }

            dsc.AppendLine("Connections:");
            foreach (WireConnection connection in Connections)
            {
                dsc.AppendLine("Connection");
                dsc.AppendLine($"Node1 - {((connection.Node1 != null) ? connection.Node1.Pos : "null")}");
                dsc.AppendLine($"Node2 - {((connection.Node2 != null) ? connection.Node2.Pos : "null")}");
                dsc.AppendLine();
            }
        }

    }
}
