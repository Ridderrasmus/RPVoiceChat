using HarmonyLib;
using RPVoiceChat.GameContent.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RPVoiceChat
{
    internal class HammerWeldingPatch
    {
        internal static void Patch(Harmony harmony)
        {
            var OriginalMethod1 = AccessTools.Method(typeof(ItemHammer), nameof(ItemHammer.OnHeldAttackStart));
            var PrefixMethod1 = AccessTools.Method(typeof(HammerWeldingPatch), nameof(OnHeldAttackStart));
            harmony.Patch(OriginalMethod1, prefix:new HarmonyMethod(PrefixMethod1));

            var OriginalMethod2 = AccessTools.Method(typeof(ItemHammer), nameof(ItemHammer.OnHeldAttackStep));
            var PrefixMethod2 = AccessTools.Method(typeof(HammerWeldingPatch), nameof(OnHeldAttackStep));
            harmony.Patch(OriginalMethod2, prefix: new HarmonyMethod(PrefixMethod2));

            var OriginalMethod3 = AccessTools.Method(typeof(ItemHammer), nameof(ItemHammer.OnHeldAttackStop));
            var PrefixMethod3 = AccessTools.Method(typeof(HammerWeldingPatch), nameof(OnHeldAttackStop));
            harmony.Patch(OriginalMethod3, prefix: new HarmonyMethod(PrefixMethod3));
        }

        public static void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BEWeldable bew)
            {
                handling = EnumHandHandling.PreventDefault;

                IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

                if (bew.TestReadyToMerge())
                {
                    byEntity.World.RegisterCallback((dt) =>
                    {
                        if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
                        {
                            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/effect/anvilmergehit"), byPlayer, byPlayer);
                        }
                    }, 464);
                    return;
                }

                return;
            }
        }

        public static bool OnHeldAttackStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            if (blockSelection == null) return false;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSelection.Position);
            if (be is BEWeldable bew && !bew.TestReadyToMerge())
            {
                return false;
            }

            return true;

        }

        public static void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || secondsPassed < 0.4f) return;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BEWeldable bew)
            {
                IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
                bew.OnHammerHitOver(byPlayer, blockSel.HitPosition);
            }
        }
    }
}