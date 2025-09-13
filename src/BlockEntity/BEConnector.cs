﻿using System.Text;
using RPVoiceChat.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityConnector : WireNode, IWireConnectable
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
            if (NetworkUID > 0)
            {
                dsc.AppendLine(UIUtils.I18n("RelayNode.NetworkId", NetworkUID));
            }
            else
            {
                dsc.AppendLine(UIUtils.I18n("RelayNode.NoNetwork"));
            }
            base.GetBlockInfo(forPlayer, dsc);
        }
    }
}
