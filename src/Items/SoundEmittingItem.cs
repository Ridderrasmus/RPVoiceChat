﻿using System;
using RPVoiceChat.Audio;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RPVoiceChat.GameContent.Items
{
    public class SoundEmittingItem : Item
    {
        private Random rand = new Random();
        public const float MaxGain = 2f;

        protected int AudibleDistance = 16;
        protected float DefaultVolume = 1f;
        private float soundDuration = 2f;
        private bool isUsable = true;
        private bool cooldownActive = false;

        private ICoreAPI api;

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
            base.OnLoaded(api);

            AudibleDistance = Attributes?["soundAudibleDistance"].AsInt(AudibleDistance) ?? AudibleDistance;
            DefaultVolume = Attributes?["soundVolume"].AsFloat(DefaultVolume) ?? DefaultVolume;
            soundDuration = Attributes?["soundDuration"].AsFloat(soundDuration) ?? soundDuration;
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

            float rawVolume = DefaultVolume * PlayerListener.ItemGain;
            float volume = Math.Clamp(rawVolume, 0f, MaxGain);

            player.Entity.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, $"sounds/{chosenSound}.ogg"),
                player,
                dualCall ? player : null,
                false,
                AudibleDistance,
                volume
            );

            StartSoundDurationCooldown();
        }

        /// <summary>
        /// Starts a cooldown timer based on the duration of the sound to prevent overlapping playback.
        /// 
        /// Note:
        /// 1) Each sound-emitting item should define a "soundDuration" value in its JSON attributes,
        ///    representing the length of the sound in seconds.
        /// 2) This method could be improved by dynamically retrieving the actual duration of the sound
        ///    using a third-party library such as NVorbis, rather than relying on a static value.
        /// </summary>
        private void StartSoundDurationCooldown()
        {
            if (cooldownActive) return;

            cooldownActive = true;
            isUsable = false;

            api.World.RegisterCallback((float dt) => {
                isUsable = true;
                cooldownActive = false;
            }, (int)(soundDuration * 1000));
        }
    }
}