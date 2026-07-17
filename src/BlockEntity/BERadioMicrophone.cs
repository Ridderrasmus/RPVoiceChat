using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioMicrophone : BEWireNode, IWireTypedNode, IRadioVoiceInput
    {
        private RadioMicrophoneDialog dialog;
        private bool isTransmitting;
        private string activeOperatorPlayerUid = "";

        public override bool IsActiveEndpoint => true;
        protected override int MaxConnections => 1;
        public WireNodeKind WireNodeKind => WireNodeKind.Radio;
        public int VoiceCaptureRangeBlocks => ServerConfigManager.RadioMicrophoneCaptureDistance;

        public bool IsTransmitting => isTransmitting;

        public string ActiveOperatorPlayerUid => activeOperatorPlayerUid ?? "";

        public bool IsBusyForOtherPlayer(string playerUid)
        {
            return isTransmitting
                && !string.IsNullOrWhiteSpace(activeOperatorPlayerUid)
                && activeOperatorPlayerUid != playerUid;
        }

        public bool IsOperator(string playerUid)
        {
            return !isTransmitting
                || string.IsNullOrWhiteSpace(activeOperatorPlayerUid)
                || activeOperatorPlayerUid == playerUid;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            OnConnectionsChanged += OnRadioWireConnectionsChanged;
            if (api.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.RegisterMicrophone(Pos);
            }
        }

        private void OnRadioWireConnectionsChanged()
        {
            if (Api is ICoreServerAPI sapi)
            {
                WireTopologyConnectivity.NotifyNode(sapi, this);
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

            dialog = new RadioMicrophoneDialog(capi, this);
            dialog.TryOpen();
            return true;
        }

        public void RequestSetTransmitting(bool enabled)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetMicrophoneTransmit,
                IntValue = enabled ? 1 : 0
            });
        }

        public bool SetTransmitting(IPlayer byPlayer, bool enabled)
        {
            if (Api?.Side != EnumAppSide.Server || byPlayer == null)
            {
                return false;
            }

            if (enabled)
            {
                if (isTransmitting
                    && !string.IsNullOrWhiteSpace(activeOperatorPlayerUid)
                    && activeOperatorPlayerUid != byPlayer.PlayerUID)
                {
                    return false;
                }

                isTransmitting = true;
                activeOperatorPlayerUid = byPlayer.PlayerUID;
            }
            else
            {
                if (isTransmitting
                    && !string.IsNullOrWhiteSpace(activeOperatorPlayerUid)
                    && activeOperatorPlayerUid != byPlayer.PlayerUID)
                {
                    return false;
                }

                ClearTransmissionInternal();
            }

            MarkDirty(true);
            return true;
        }

        public void ClearTransmission()
        {
            if (!isTransmitting && string.IsNullOrWhiteSpace(activeOperatorPlayerUid))
            {
                return;
            }

            string previousOperator = activeOperatorPlayerUid;
            ClearTransmissionInternal();
            MarkDirty(true);

            if (Api?.Side == EnumAppSide.Server && !string.IsNullOrWhiteSpace(previousOperator))
            {
                Api.ModLoader.GetModSystem<RadioVoiceRoutingSystem>()?.ClearMicRoute(previousOperator);
            }
        }

        private void ClearTransmissionInternal()
        {
            isTransmitting = false;
            activeOperatorPlayerUid = "";
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            isTransmitting = tree.GetBool("rpvc:radioMicTransmitting", false);
            activeOperatorPlayerUid = tree.GetString("rpvc:radioMicOperatorUid", "");
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("rpvc:radioMicTransmitting", isTransmitting);
            tree.SetString("rpvc:radioMicOperatorUid", activeOperatorPlayerUid ?? "");
        }

        public override void OnBlockRemoved()
        {
            ClearTransmission();
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterMicrophone(Pos);
            }

            base.OnBlockRemoved();
            dialog?.TryClose();
        }

        public override void OnBlockUnloaded()
        {
            ClearTransmission();
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterMicrophone(Pos);
            }

            base.OnBlockUnloaded();
        }
    }
}
