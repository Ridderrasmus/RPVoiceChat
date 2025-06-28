using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RPVoiceChat.GameContent.Items
{
    public class SoundEmittingItem : Item
    {
        private Random rand = new Random();

        protected int AudibleDistance = 16;
        protected float DefaultVolume = 0.6f;
        protected int CooldownTime = 2;

        private bool isUsable = true;
        private int time = 0;
        private long cooldownThreadID = 0;
        private ICoreAPI api;

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
            base.OnLoaded(api);

            AudibleDistance = Attributes?["soundAudibleDistance"].AsInt(AudibleDistance) ?? AudibleDistance;
            DefaultVolume = Attributes?["soundVolume"].AsFloat(DefaultVolume) ?? DefaultVolume;
            CooldownTime = Attributes?["cooldownTime"].AsInt(CooldownTime) ?? CooldownTime;
        }

        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot slot)
        {
            base.OnAttackingWith(world, byEntity, attackedEntity, slot);
            if (!world.Side.IsServer()) return;

            if (byEntity is EntityPlayer player)
            {
                PlaySound("rightClickSounds", player.World.PlayerByUid(player.PlayerUID));
            }
        }

        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot slot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            if (!world.Side.IsServer()) return base.OnBlockBrokenWith(world, byEntity, slot, blockSel, dropQuantityMultiplier);

            if (byEntity is EntityPlayer player)
            {
                PlaySound("breakBlockSounds", player.World.PlayerByUid(player.PlayerUID));
            }

            return base.OnBlockBrokenWith(world, byEntity, slot, blockSel, dropQuantityMultiplier);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            IWorldAccessor world = byEntity.World;
            if (!world.Side.IsServer()) return;

            if (byEntity is EntityPlayer player)
            {
                PlaySound("rightClickSounds", player.World.PlayerByUid(player.PlayerUID));
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.Handled;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return true;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return true;
        }

        private void PlaySound(string soundKey, IPlayer player)
        {
            PlaySound(soundKey, player, dualCall: false);
        }

        private void PlaySound(string soundKey, IPlayer player, bool dualCall)
        {
            if (!isUsable || player == null) return;

            string[] soundList = Attributes?[soundKey].AsArray<string>(Array.Empty<string>());
            if (soundList == null || soundList.Length == 0) return;

            string chosenSound = soundList[rand.Next(soundList.Length)];

            // Volume configurable
            float volume = ClientSettings.OutputItem != 0 ? ClientSettings.OutputItem : DefaultVolume;

            player.Entity.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, $"sounds/{chosenSound}.ogg"),
                player,
                dualCall ? player : null,
                false,
                AudibleDistance,
                volume
            );

            StartCooldown();
        }

        private void StartCooldown()
        {
            if (cooldownThreadID == 0 && api != null)
            {
                cooldownThreadID = api.Event.RegisterGameTickListener(CooldownTick, 1000);
            }
        }

        private void CooldownTick(float dt)
        {
            time++;

            if (time >= CooldownTime || time < 0)
            {
                api.Event.UnregisterGameTickListener(cooldownThreadID);
                cooldownThreadID = 0;
                time = 0;
                isUsable = true;
            }
        }
    }
}