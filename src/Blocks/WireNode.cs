using RPVoiceChat.Systems;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Blocks
{
    public class WireNode : BlockEntity
    {

        public int NetworkUID { get; set; } = 0;
        public string NodeUID => Pos.ToString();

        protected EventHandler<string> OnRecievedSignalEvent { get; set; }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
                return;

            WireNetworkHandler.GetNetwork(NetworkUID).AddNode(this);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            // For now we will just add every new node to the same network
            //WireNetworkHandler.AddNewNetwork(this);

            if (Api.Side == EnumAppSide.Client)
                return;

            WireNetworkHandler.GetNetwork(NetworkUID).AddNode(this);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Client)
                return;

            WireNetworkHandler.GetNetwork(NetworkUID).RemoveNode(this);
        }

        public virtual void OnRecievedSignal(string message)
        {
            OnRecievedSignalEvent?.Invoke(this, message);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            NetworkUID = tree.GetInt("rpvc:networkUID");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("rpvc:networkUID", NetworkUID);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"NetworkUID: {NetworkUID}");
            dsc.AppendLine($"NodeUID: {NodeUID}");

            dsc.AppendLine("Nodes in network:");
            foreach (WireNode node in WireNetworkHandler.GetNetwork(NetworkUID).nodes)
            {
                dsc.AppendLine($"Node: {node.NodeUID}");
            }
        }

    }
}
