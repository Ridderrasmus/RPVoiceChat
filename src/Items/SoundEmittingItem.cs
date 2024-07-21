using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RPVoiceChat.GameContent.Items
{
    public class SoundEmittingItem : Item
    {
        private Random Random = new Random();

        protected int AudibleDistance = 16;
        protected float Volume = 0.6f;
        protected int CooldownTime = 2;

        private bool isUsable = true;
        private int time = 0;

        private long cooldownThreadID = 0;

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
            base.OnLoaded(api);

            AudibleDistance = (int)(Attributes?["soundAudibleDistance"].AsInt(AudibleDistance));
            Volume = (float)(Attributes?["soundVolume"].AsFloat(Volume));
            CooldownTime = (int)(Attributes?["cooldownTime"].AsInt(CooldownTime));
        }


        // When an entity is attacked with this item
        // Called twice it seems. Both clientside and serverside?
        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot slot)
        {
            base.OnAttackingWith(world, byEntity, attackedEntity, slot);
            if (!world.Side.IsServer()) return;


            if (byEntity is EntityPlayer)
                PlaySound("rightClickSounds", byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID));
        }

        // When a block is broken with this item
        // Called twice it seems. Both clientside and serverside?
        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot slot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            if (!world.Side.IsServer()) return base.OnBlockBrokenWith(world, byEntity, slot, blockSel, dropQuantityMultiplier);

            if (byEntity is EntityPlayer)
                PlaySound("breakBlockSounds", byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID));

            return base.OnBlockBrokenWith(world, byEntity, slot, blockSel, dropQuantityMultiplier);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            IWorldAccessor world = byEntity.World;
            if (!world.Side.IsServer()) return;

            if (byEntity is EntityPlayer)
                PlaySound("rightClickSounds", byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID));
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

        private void PlaySound(string soundSource, IPlayer player)
        {
            PlaySound(soundSource, player, false);
        }

        private void PlaySound(string soundSource, IPlayer player, bool dualCall)
        {
            if (!isUsable) return;
            isUsable = false;

            if (player == null) return;

            string[] soundList = Attributes?[soundSource].AsArray<string>(new string[0]);

            if (soundList == null || soundList.Length == 0) return;

            string sound = soundList[Random.Next(soundList.Length)];

            if (dualCall)
                player.Entity.World.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/" + sound + ".ogg"), player, player, false, AudibleDistance, Volume);
            else
                player.Entity.World.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/" + sound + ".ogg"), player, null, false, AudibleDistance, Volume);

            StartCountdown();
        }


        private void StartCountdown()
        {
            if (cooldownThreadID == 0)
                cooldownThreadID = api.Event.RegisterGameTickListener(Cooldown, 1000);
        }

        private void Cooldown(float dt)
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
