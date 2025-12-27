using System.Text;
using RPVoiceChat.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityConnector : BEWireNode, IWireConnectable
    {

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            // Can be assigned by WireItem
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            // Custom network status display
            DisplayNetworkStatus(forPlayer, dsc);

            // Display connections (legacy)
            DisplayConnections(forPlayer, dsc);
        }

        protected override void DisplayNetworkStatus(IPlayer forPlayer, StringBuilder dsc)
        {
            if (NetworkUID > 0)
            {
                dsc.AppendLine(UIUtils.I18n("RelayNode.NetworkId", NetworkUID));
            }
            else
            {
                dsc.AppendLine(UIUtils.I18n("RelayNode.NoNetwork"));
            }
        }
    }
}
