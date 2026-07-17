using RPVoiceChat.Gui;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioReceiver : Vintagestory.API.Common.BlockEntity
    {
        private RadioReceiverDialog dialog;
        private string tunedFrequency = "100.0";

        public string TunedFrequency => tunedFrequency ?? "";

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.RegisterReceiver(Pos);
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

            dialog = new RadioReceiverDialog(capi, this);
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
                Operation = RadioSettingsOperation.SetReceiverFrequency,
                Value = desired ?? ""
            });
        }

        public void SetTunedFrequency(string desired)
        {
            tunedFrequency = (desired ?? "").Trim();
            MarkDirty();
            dialog?.RefreshData();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            tunedFrequency = tree.GetString("rpvc:radioReceiverFrequency", tunedFrequency);
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("rpvc:radioReceiverFrequency", tunedFrequency ?? "");
        }

        public override void OnBlockRemoved()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterReceiver(Pos);
            }

            base.OnBlockRemoved();
            dialog?.TryClose();
        }

        public override void OnBlockUnloaded()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterReceiver(Pos);
            }

            base.OnBlockUnloaded();
        }
    }
}
