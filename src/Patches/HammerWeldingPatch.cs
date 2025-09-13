using System;
using HarmonyLib;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.src.Networking.Packets;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RPVoiceChat
{
    internal class HammerWeldingPatch
    {
        internal static void Patch(Harmony harmony)
        {
            var originalOnHeldAttackStart = AccessTools.Method(typeof(ItemHammer), nameof(ItemHammer.OnHeldAttackStart));
            var prefixOnHeldAttackStart = AccessTools.Method(typeof(HammerWeldingPatch), nameof(OnHeldAttackStart));
            harmony.Patch(originalOnHeldAttackStart, prefix: new HarmonyMethod(prefixOnHeldAttackStart));

            var originalOnHeldAttackStep = AccessTools.Method(typeof(ItemHammer), nameof(ItemHammer.OnHeldAttackStep));
            var prefixOnHeldAttackStep = AccessTools.Method(typeof(HammerWeldingPatch), nameof(OnHeldAttackStep));
            harmony.Patch(originalOnHeldAttackStep, prefix: new HarmonyMethod(prefixOnHeldAttackStep));
        }

        /// <summary>
        /// Handles detection of a BEWeldable and triggers the welding behavior.
        /// Otherwise, lets the vanilla behavior proceed.
        /// </summary>
        public static bool OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null)
            {
                return true; // vanilla continue
            }

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is not BEWeldable bew)
            {
                return true; // vanilla continue
            }

            handling = EnumHandHandling.PreventDefault;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null)
            {
                return false;
            }

            bool wasValid = bew.TestReadyToMerge();

            strikeBell(byEntity, slot, slot.Itemstack);

            if (wasValid)
            {
                byEntity.World.RegisterCallback((dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
                    {
                        byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/effect/anvilmergehit"), byPlayer, byPlayer);
                    }
                }, 464);
            }

            return false;
        }

        public static bool OnHeldAttackStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            if (blockSelection == null)
            {
                return true; // vanilla continue
            }

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSelection.Position);
            if (be is not BEWeldable bew)
            {
                return true; // vanilla continue
            }

            if (!bew.TestReadyToMerge())
            {
                return false;
            }

            return true;
        }

        protected static void strikeBell(EntityAgent byEntity, ItemSlot slot, ItemStack strikingItem)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            var blockSel = byPlayer.CurrentBlockSelection;
            if (blockSel == null) return;

            if (strikingItem != byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack) return;

            // Send packet to server
            if (RPVoiceChatMod.ClientChannel != null)
            {
                RPVoiceChatMod.ClientChannel.SendPacket(new WeldingHitPacket
                {
                    Pos = blockSel.Position,
                    HitPosition = blockSel.HitPosition
                });
            }

            slot.Itemstack?.TempAttributes.SetBool("isBellAction", false);
        }


    }
}
