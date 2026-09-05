using System.Collections.Generic;
using System.Linq;
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
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.GameContent.Items
{
    public class ItemRadio : Item
    {
        public const string TunedFrequencyAttribute = "rpvc:tunedFrequency";
        public const string InventoryListenAttribute = "rpvc:inventoryListen";
        public const string ListenVolumeAttribute = "rpvc:listenVolume";
        public const int MinListenVolumePercent = 0;
        public const int MaxListenVolumePercent = 100;
        public const int DefaultListenVolumePercent = 100;
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
            if (GetInventoryListen(inSlot?.Itemstack))
            {
                dsc.AppendLine(UIUtils.I18n("Radio.Talkie.Info.InventoryListen"));
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = RPVoiceChatMod.modID + ":Radio.Talkie.Interaction.Tune",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>
                        bs == null
                        && es == null
                        && api is ICoreClientAPI capi
                        && capi.World?.Player?.Entity?.Controls?.Sneak == true
                }
            };
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
                    RadioTalkieDialog.OpenOrFocus(capi, slot);
                }

                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (byEntity.Api is ICoreClientAPI capiClient && byEntity == capiClient.World.Player.Entity)
            {
                RadioTalkieDialog.CloseIfOpen();

                var microphoneManager = RPVoiceChatClient.MicrophoneManagerInstance;
                if (microphoneManager != null)
                {
                    microphoneManager.SetTransmissionRange(ServerConfigManager.RadioTalkieRangeBlocks);
                    microphoneManager.SetTalkieHeldTransmit(true);
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
                microphoneManager?.SetTalkieHeldTransmit(false);
                microphoneManager?.ResetTransmissionRange();
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

        public static IEnumerable<string> GetActiveListenFrequencies(IPlayer player)
        {
            if (player?.Entity == null)
            {
                return Enumerable.Empty<string>();
            }

            if (player is IServerPlayer && player.Entity?.Api is ICoreServerAPI sapi)
            {
                RadioTalkieTransmissionSystem talkieTransmission = sapi.ModLoader.GetModSystem<RadioTalkieTransmissionSystem>();
                if (talkieTransmission != null && talkieTransmission.IsTalkieTransmitting(player.PlayerUID))
                {
                    return Enumerable.Empty<string>();
                }
            }

            try
            {
                EntityAgent entity = player.Entity;
                if (!entity.Alive)
                {
                    return Enumerable.Empty<string>();
                }

                var frequencies = new HashSet<string>();
                CollectInventoryListenFrequencies(player, frequencies);
                CollectListenFrequency(entity.RightHandItemSlot, fromHands: true, frequencies);
                CollectListenFrequency(entity.LeftHandItemSlot, fromHands: true, frequencies);

                return frequencies;
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        public static float GetLocalTalkieListenVolumeGain(ICoreClientAPI capi)
        {
            IPlayer player = capi?.World?.Player;
            if (player?.Entity == null)
            {
                return 1f;
            }

            float maxGain = 0f;
            bool found = false;
            EntityAgent entity = player.Entity;
            ConsiderListenVolume(entity.RightHandItemSlot, fromHands: true, ref maxGain, ref found);
            ConsiderListenVolume(entity.LeftHandItemSlot, fromHands: true, ref maxGain, ref found);

            if (player.InventoryManager != null)
            {
                ConsiderInventoryListenVolume(player.InventoryManager.GetOwnInventory("hotbar"), ref maxGain, ref found);
                ConsiderInventoryListenVolume(player.InventoryManager.GetOwnInventory("backpack"), ref maxGain, ref found);
            }

            return found ? maxGain : 1f;
        }

        public static string GetTunedFrequency(IPlayer player)
        {
            foreach (string frequency in GetActiveListenFrequencies(player))
            {
                return frequency;
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

        public static bool GetInventoryListen(ItemStack stack)
        {
            return stack?.Attributes != null && stack.Attributes.GetBool(InventoryListenAttribute, false);
        }

        public static void SetInventoryListen(ItemStack stack, bool enabled)
        {
            if (stack?.Attributes == null)
            {
                return;
            }

            stack.Attributes.SetBool(InventoryListenAttribute, enabled);
        }

        public static int GetListenVolumePercent(ItemStack stack)
        {
            if (stack?.Attributes == null)
            {
                return DefaultListenVolumePercent;
            }

            return GameMath.Clamp(
                stack.Attributes.GetInt(ListenVolumeAttribute, DefaultListenVolumePercent),
                MinListenVolumePercent,
                MaxListenVolumePercent);
        }

        public static void SetListenVolumePercent(ItemStack stack, int volumePercent)
        {
            if (stack?.Attributes == null)
            {
                return;
            }

            stack.Attributes.SetInt(
                ListenVolumeAttribute,
                GameMath.Clamp(volumePercent, MinListenVolumePercent, MaxListenVolumePercent));
        }

        public static bool TryApplyServerSettings(
            IServerPlayer player,
            int slotNumber,
            string frequency,
            bool inventoryListen,
            int listenVolumePercent)
        {
            if (player?.InventoryManager == null)
            {
                return false;
            }

            IInventory hotbar = player.InventoryManager.GetOwnInventory("hotbar");
            if (hotbar == null)
            {
                return false;
            }

            ItemSlot slot = FindTalkieHotbarSlot(hotbar, slotNumber);
            if (slot == null)
            {
                return false;
            }

            SetTunedFrequency(slot.Itemstack, frequency);
            SetInventoryListen(slot.Itemstack, inventoryListen);
            SetListenVolumePercent(slot.Itemstack, listenVolumePercent);
            slot.MarkDirty();
            return true;
        }

        public static void SendTalkieSettings(ICoreClientAPI capi, ItemSlot slot)
        {
            if (capi == null || slot?.Itemstack == null)
            {
                return;
            }

            SendTalkieSettings(
                capi,
                slot,
                GetTunedFrequency(slot.Itemstack),
                GetInventoryListen(slot.Itemstack),
                GetListenVolumePercent(slot.Itemstack));
        }

        public static void SendTalkieSettings(
            ICoreClientAPI capi,
            ItemSlot slot,
            string frequency,
            bool inventoryListen,
            int listenVolumePercent)
        {
            if (capi == null || slot == null)
            {
                return;
            }

            int hotbarSlotNumber = capi.World.Player.InventoryManager.ActiveHotbarSlotNumber;
            if (!IsTalkieStack(capi.World.Player.InventoryManager.ActiveHotbarSlot))
            {
                hotbarSlotNumber = ResolveHotbarSlotIndex(slot, capi.World.Player.InventoryManager.GetOwnInventory("hotbar"));
            }

            RPVoiceChatMod.RadioTalkieClientChannel?.SendPacket(new RadioTalkieSettingsPacket
            {
                Frequency = RadioFrequencyUtil.Normalize(frequency),
                InventoryListen = inventoryListen,
                SlotNumber = hotbarSlotNumber,
                ListenVolumePercent = GameMath.Clamp(listenVolumePercent, MinListenVolumePercent, MaxListenVolumePercent)
            });
        }

        private static int ResolveHotbarSlotIndex(ItemSlot slot, IInventory hotbar)
        {
            if (slot == null || hotbar == null)
            {
                return -1;
            }

            if (slot.Inventory == hotbar)
            {
                for (int slotIndex = 0; slotIndex < hotbar.Count; slotIndex++)
                {
                    if (hotbar[slotIndex] == slot)
                    {
                        return slotIndex;
                    }
                }
            }

            for (int slotIndex = 0; slotIndex < hotbar.Count; slotIndex++)
            {
                if (hotbar[slotIndex] == slot)
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private static ItemSlot FindTalkieHotbarSlot(IInventory hotbar, int preferredSlotNumber)
        {
            if (hotbar == null)
            {
                return null;
            }

            if (preferredSlotNumber >= 0
                && preferredSlotNumber < hotbar.Count
                && IsTalkieStack(hotbar[preferredSlotNumber]))
            {
                return hotbar[preferredSlotNumber];
            }

            for (int slotIndex = 0; slotIndex < hotbar.Count; slotIndex++)
            {
                if (IsTalkieStack(hotbar[slotIndex]))
                {
                    return hotbar[slotIndex];
                }
            }

            return null;
        }

        private static void CollectListenFrequency(ItemSlot slot, bool fromHands, HashSet<string> frequencies)
        {
            if (!IsTalkieStack(slot))
            {
                return;
            }

            if (!fromHands && !GetInventoryListen(slot.Itemstack))
            {
                return;
            }

            string frequency = GetTunedFrequency(slot.Itemstack);
            if (!string.IsNullOrEmpty(frequency))
            {
                frequencies.Add(frequency);
            }
        }

        private static void CollectInventoryListenFrequencies(IPlayer player, HashSet<string> frequencies)
        {
            if (player?.InventoryManager == null)
            {
                return;
            }

            CollectInventorySlots(player.InventoryManager.GetOwnInventory("hotbar"), frequencies);
            CollectInventorySlots(player.InventoryManager.GetOwnInventory("backpack"), frequencies);
        }

        private static void CollectInventorySlots(IInventory inventory, HashSet<string> frequencies)
        {
            if (inventory == null)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < inventory.Count; slotIndex++)
            {
                CollectListenFrequency(inventory[slotIndex], fromHands: false, frequencies);
            }
        }

        private static void ConsiderListenVolume(ItemSlot slot, bool fromHands, ref float maxGain, ref bool found)
        {
            if (!IsTalkieStack(slot))
            {
                return;
            }

            if (!fromHands && !GetInventoryListen(slot.Itemstack))
            {
                return;
            }

            found = true;
            maxGain = System.Math.Max(maxGain, GetListenVolumePercent(slot.Itemstack) / 100f);
        }

        private static void ConsiderInventoryListenVolume(IInventory inventory, ref float maxGain, ref bool found)
        {
            if (inventory == null)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < inventory.Count; slotIndex++)
            {
                ConsiderListenVolume(inventory[slotIndex], fromHands: false, ref maxGain, ref found);
            }
        }

        private static bool IsTalkieStack(ItemSlot slot)
        {
            ItemStack stack = slot?.Itemstack;
            return stack?.Collectible?.Code != null
                && stack.Collectible.Code.Domain == RPVoiceChatMod.modID
                && stack.Collectible.Code.Path == "radiotalkie";
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
