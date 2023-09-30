using System;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class SoundEmittingBlock : Block
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
            base.OnLoaded(api);

            AudibleDistance = (int)(Attributes?["soundAudibleDistance"].AsInt(AudibleDistance));
            Volume = (float)(Attributes?["soundVolume"].AsFloat(Volume));
            CooldownTime = (int)(Attributes?["cooldownTime"].AsInt(CooldownTime));
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            return true;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!world.Side.IsServer()) return;


            PlaySoundAt("blockInteractSounds", blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);

            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        private void PlaySoundAt(string soundSource, double x, double y, double z)
        {
            PlaySoundAt(soundSource, x, y, z, null);
        }

        private void PlaySoundAt(string soundSource, double x, double y, double z, IPlayer player)
        {
            if (!isUsable) return;
            isUsable = false;

            string[] soundList = Attributes?[soundSource].AsArray<string>(new string[0]);

            if (soundList == null || soundList.Length == 0) return;

            string sound = soundList[Random.Next(soundList.Length)];

            if (player == null)
                api.World.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/" + sound + ".ogg"), x, y, z, null, false, AudibleDistance, Volume);
            else
                api.World.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/" + sound + ".ogg"), x, y, z, player, false, AudibleDistance, Volume);

            StartCountdown();
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