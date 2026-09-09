using System;
using RPVoiceChat;
using RPVoiceChat.Audio;
using RPVoiceChat.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RPVoiceChat.GameContent.BlockEntityBehavior
{
    /// <summary>
    /// Block entity behavior that plays a sound when the block is rung (interaction completed).
    /// Reads sound list, volume, duration and distance from the block's attributes.
    /// Add this behavior to any block entity that should play a sound on interact (callbell, carillonbell, churchbell).
    /// </summary>
    public class BEBehaviorSoundable : Vintagestory.API.Common.BlockEntityBehavior, IBlockEntityRungBehavior
    {
        private readonly Random _random = new Random();
        private bool _isUsable = true;
        private bool _cooldownActive = false;

        public const float MaxGain = 2f;

        public BEBehaviorSoundable(Vintagestory.API.Common.BlockEntity blockEntity) : base(blockEntity)
        {
        }

        public void OnRung()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            PlaySoundAtBlock();
        }

        private void PlaySoundAtBlock()
        {
            if (!_isUsable) return;

            var block = Blockentity.Block;
            if (block?.Attributes == null) return;

            string[] soundList = block.Attributes["blockInteractSounds"].AsArray<string>(Array.Empty<string>());
            if (soundList == null || soundList.Length == 0) return;

            _isUsable = false;

            string sound = soundList[_random.Next(soundList.Length)];
            float defaultVolume = block.Attributes["soundVolume"].AsFloat(1f);
            float rawVolume = defaultVolume * PlayerListener.BlockGain;
            float finalVolume = Math.Clamp(rawVolume, 0f, MaxGain);
            int audibleDistance = GetAudibleDistance(block);

            Api.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/" + sound + ".ogg"),
                Blockentity.Pos.X + 0.5, Blockentity.Pos.Y + 0.5, Blockentity.Pos.Z + 0.5,
                null,
                false,
                audibleDistance,
                finalVolume
            );

            float soundDuration = block.Attributes["soundDuration"].AsFloat(2f);
            StartSoundDurationCooldown(soundDuration);
        }

        private int GetAudibleDistance(Vintagestory.API.Common.Block block)
        {
            int fromAttributes = block.Attributes?["soundAudibleDistance"].AsInt(16) ?? 16;
            if (Api?.Side != EnumAppSide.Server) return fromAttributes;

            string blockCode = block.Code?.Path?.ToLowerInvariant() ?? "";
            if (blockCode.StartsWith("callbell")) return ServerConfigManager.CallbellAudibleDistance;
            if (blockCode.StartsWith("carillonbell")) return ServerConfigManager.CarillonbellAudibleDistance;
            if (blockCode.StartsWith("churchbell")) return ServerConfigManager.ChurchbellAudibleDistance;
            return fromAttributes;
        }

        private void StartSoundDurationCooldown(float soundDurationSeconds)
        {
            if (_cooldownActive) return;
            _cooldownActive = true;
            Api.World.RegisterCallback(_ =>
            {
                _isUsable = true;
                _cooldownActive = false;
            }, (int)(soundDurationSeconds * 1000));
        }
    }
}
