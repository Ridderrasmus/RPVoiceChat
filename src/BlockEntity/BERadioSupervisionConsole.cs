using System.Linq;
using System.Text;
using RPVoiceChat.Gui;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioSupervisionConsole : BEWireNode, INetworkRoot, IWireTypedNode
    {
        private RadioConsoleDialog dialog;
        private string frequency = "100.0";
        private string displayName = "";
        private long originalCreatedNetworkID;

        public override bool IsActiveEndpoint => true;
        protected override int MaxConnections => 2;
        public WireNodeKind WireNodeKind => WireNodeKind.RadioConsole;
        public long CreatedNetworkID => originalCreatedNetworkID;

        public string Frequency => frequency ?? "";
        public string DisplayName => displayName ?? "";

        public override void OnNetworkCreated(long networkID)
        {
            base.OnNetworkCreated(networkID);
            if (originalCreatedNetworkID == 0)
            {
                originalCreatedNetworkID = networkID;
                MarkDirty();
            }
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                return true;
            }

            if (Api is not ICoreClientAPI capi)
            {
                return true;
            }

            if (dialog?.IsOpened() == true)
            {
                return true;
            }

            dialog = new RadioConsoleDialog(capi, this);
            dialog.TryOpen();
            return true;
        }

        public void RequestSetFrequency(string desired)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetFrequency,
                Value = desired ?? ""
            });
        }

        public void RequestSetDisplayName(string desired)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetDisplayName,
                Value = desired ?? ""
            });
        }

        public void RequestSaveSettings(string desiredFrequency, string desiredDisplayName)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RequestSetFrequency(desiredFrequency);
            RequestSetDisplayName(desiredDisplayName);
        }

        public void SetFrequency(string desired)
        {
            frequency = (desired ?? "").Trim();
            MarkDirty();
            dialog?.RefreshData();
        }

        public void SetDisplayName(string desired)
        {
            displayName = (desired ?? "").Trim();
            MarkDirty();
            dialog?.RefreshData();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            frequency = tree.GetString("rpvc:radioFrequency", frequency);
            displayName = tree.GetString("rpvc:radioDisplayName", displayName);
            originalCreatedNetworkID = tree.GetLong("rpvc:radioConsoleCreatedNetworkId", originalCreatedNetworkID);
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("rpvc:radioFrequency", frequency ?? "");
            tree.SetString("rpvc:radioDisplayName", displayName ?? "");
            if (originalCreatedNetworkID != 0)
            {
                tree.SetLong("rpvc:radioConsoleCreatedNetworkId", originalCreatedNetworkID);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            dialog?.TryClose();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine(UIUtils.I18n("blockdesc-radioconsole-*"));
        }
    }
}
