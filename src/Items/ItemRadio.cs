using System.Text;
using RPVoiceChat;
using RPVoiceChat.Config;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace RPVoiceChat.GameContent.Items
{
    public class ItemRadio : Item
    {
        public const string TunedFrequencyAttribute = "rpvc:tunedFrequency";
        private const string DefaultFrequency = "100.0";

        private bool isTransmitting;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            dsc.AppendLine(UIUtils.I18n("Radio.Talkie.Info.Frequency", GetTunedFrequency(inSlot?.Itemstack)));
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (byEntity.Controls.Sneak)
            {
                if (byEntity.Api is ICoreClientAPI capi && byEntity == capi.World.Player.Entity)
                {
                    new RadioTalkieDialog(capi, slot).TryOpen();
                }

                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (byEntity.Api is ICoreClientAPI capiClient && byEntity == capiClient.World.Player.Entity)
            {
                var microphoneManager = RPVoiceChatClient.MicrophoneManagerInstance;
                if (microphoneManager != null)
                {
                    microphoneManager.SetVoiceLevel(VoiceLevel.Shouting);
                    microphoneManager.SetTransmissionRange(ServerConfigManager.RadioTalkieRangeBlocks);
                }

                isTransmitting = true;
                SendTalkieState(capiClient, slot, true);
            }

            handling = EnumHandHandling.PreventDefault;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!isTransmitting)
            {
                return;
            }

            if (byEntity.Api is ICoreClientAPI capi && byEntity == capi.World.Player.Entity)
            {
                var microphoneManager = RPVoiceChatClient.MicrophoneManagerInstance;
                microphoneManager?.SetVoiceLevel(VoiceLevel.Talking);
                microphoneManager?.SetTransmissionRange(WorldConfig.GetInt(VoiceLevel.Talking));
                SendTalkieState(capi, slot, false);
            }

            isTransmitting = false;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return isTransmitting;
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (isTransmitting)
            {
                OnHeldInteractStop(0f, slot, byEntity, null, null);
            }
        }

        public static string GetTunedFrequency(ItemStack stack)
        {
            if (stack?.Attributes == null)
            {
                return DefaultFrequency;
            }

            return RadioFrequencyUtil.Normalize(stack.Attributes.GetString(TunedFrequencyAttribute, DefaultFrequency));
        }

        public static string GetTunedFrequency(IPlayer player)
        {
            if (player?.Entity == null)
            {
                return "";
            }

            ItemSlot right = player.Entity.RightHandItemSlot;
            if (IsTalkieStack(right))
            {
                return GetTunedFrequency(right.Itemstack);
            }

            ItemSlot left = player.Entity.LeftHandItemSlot;
            if (IsTalkieStack(left))
            {
                return GetTunedFrequency(left.Itemstack);
            }

            return "";
        }

        public static bool IsTalkieActiveInHands(IPlayer player)
        {
            if (player?.Entity == null)
            {
                return false;
            }

            return IsTalkieStack(player.Entity.RightHandItemSlot) || IsTalkieStack(player.Entity.LeftHandItemSlot);
        }

        public static void SetTunedFrequency(ItemStack stack, string frequency)
        {
            if (stack == null)
            {
                return;
            }

            stack.Attributes.SetString(TunedFrequencyAttribute, RadioFrequencyUtil.Normalize(frequency));
        }

        private static bool IsTalkieStack(ItemSlot slot)
        {
            return slot?.Itemstack?.Collectible is ItemRadio;
        }

        private void SendTalkieState(ICoreClientAPI capi, ItemSlot slot, bool transmitting)
        {
            RPVoiceChatMod.RadioTalkieClientChannel?.SendPacket(new RadioTalkieStatePacket
            {
                Transmitting = transmitting,
                Frequency = GetTunedFrequency(slot?.Itemstack)
            });
        }
    }
}
