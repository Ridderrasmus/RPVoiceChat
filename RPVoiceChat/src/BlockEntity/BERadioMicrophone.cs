using System;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioMicrophone : BEWireNode, IWireTypedNode, IRadioVoiceInput
    {
        private const string TransmitAnimationCode = "playing-sound";

        private RadioMicrophoneDialog dialog;
        private bool isTransmitting;
        private string activeOperatorPlayerUid = "";

        public override bool IsActiveEndpoint => true;
        protected override int MaxConnections => 1;
        public WireNodeKind WireNodeKind => WireNodeKind.Radio;
        public int VoiceCaptureRangeBlocks => ServerConfigManager.RadioMicrophoneCaptureDistance;

        public bool IsTransmitting => isTransmitting;

        public string ActiveOperatorPlayerUid => activeOperatorPlayerUid ?? "";

        private BEBehaviorAnimatable Animatable => GetBehavior<BEBehaviorAnimatable>();
        private BlockEntityAnimationUtil AnimUtil => Animatable?.animUtil;

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
            else if (api.Side == EnumAppSide.Client)
            {
                SyncTransmitAnimationState();
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
            SyncTransmitAnimationState();
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

        private void SyncTransmitAnimationState()
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            InitializeClientAnimator();

            var animUtil = AnimUtil;
            if (animUtil == null)
            {
                return;
            }

            if (isTransmitting)
            {
                StartAnimationIfNotRunning(TransmitAnimationCode);
            }
            else
            {
                StopAnimation(TransmitAnimationCode);
            }
        }

        private void InitializeClientAnimator()
        {
            var animUtil = AnimUtil;
            if (animUtil == null || animUtil.animator != null || Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            string shapePath = Block?.Shape?.Base?.Path ?? "block/radiomicrophone";
            if (Block?.Code != null && !string.IsNullOrWhiteSpace(shapePath))
            {
                var assetLoc = new AssetLocation(Block.Code.Domain, "shapes/" + shapePath + ".json");
                var shape = Shape.TryGet(Api, assetLoc);
                if (shape?.Animations != null && shape.Animations.Length > 0)
                {
                    shape.InitForAnimations(Api.Logger, shapePath, Array.Empty<string>());
                }
            }

            animUtil.InitializeAnimator(shapePath, null, null, new Vec3f(0, GetBlockSideRotY(), 0));
        }

        private void StartAnimationIfNotRunning(string animationCode)
        {
            var animUtil = AnimUtil;
            if (animUtil == null || animUtil.activeAnimationsByAnimCode.ContainsKey(animationCode))
            {
                return;
            }

            animUtil.StartAnimation(new AnimationMetaData
            {
                Animation = animationCode,
                Code = animationCode
            });
        }

        private void StopAnimation(string animationCode)
        {
            AnimUtil?.StopAnimation(animationCode);
        }

        private float GetBlockSideRotY()
        {
            return Block?.Variant?.TryGetValue("side", out string side) == true
                ? side switch
                {
                    "north" => 0f,
                    "east" => 270f,
                    "west" => 90f,
                    "south" => 180f,
                    _ => 0f
                }
                : 0f;
        }
    }
}
