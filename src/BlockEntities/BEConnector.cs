using System.Text;
using RPVoiceChat.GameContent.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

public class BEConnector : WireNode, IWireConnectable
{

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        // Can be assigned by WireItem
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
        dsc.AppendLine($"Relay node - Network {NetworkUID}");
        base.GetBlockInfo(forPlayer, dsc);
    }
}
