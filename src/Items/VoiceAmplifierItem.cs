using System;
using RPVoiceChat.Audio;
using RPVoiceChat.Audio.Effects;
using RPVoiceChat.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Items
{
    public class VoiceAmplifierItem : Item
    {
        private string soundEffectName = null;
        private SoundEffect currentEffect;

        public AudioOutputManager audioOutputManager;

        private bool isAmplifierActive = false;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            var attr = Attributes;
            if (attr != null && attr["applySoundEffect"]?.Exists == true)
            {
                soundEffectName = attr["applySoundEffect"].AsString(null);
            }

            if (api.Side == EnumAppSide.Client)
            {
                audioOutputManager = RPVoiceChatClient.AudioOutputManagerInstance;
                if (audioOutputManager == null)
                {
                    Logger.client.Warning("audioOutputManager is null in VoiceAmplifierItem.OnLoaded");
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (isAmplifierActive)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (byEntity.Api is ICoreClientAPI capi && byEntity == capi.World.Player.Entity)
            {
                VoiceLevel voiceLevel = VoiceLevel.Talking;
                float voiceRangeBoost = 0f; // Default: no boost

                var attr = slot?.Itemstack?.Collectible?.Attributes;
                if (attr != null)
                {
                    if (attr["voiceLevel"]?.Exists == true)
                    {
                        string voiceLevelStr = attr["voiceLevel"].AsString("Talking");
                        if (Enum.TryParse(voiceLevelStr, ignoreCase: true, out VoiceLevel parsedLevel))
                        {
                            voiceLevel = parsedLevel;
                        }
                    }

                    if (attr["voiceRangeBoost"]?.Exists == true)
                    {
                        voiceRangeBoost = attr["voiceRangeBoost"].AsFloat(0f) / 100f;
                    }
                }

                audioOutputManager?.SetVoiceLevelForPlayer(capi.World.Player.PlayerUID, voiceLevel);

                if (voiceRangeBoost != 0f)
                {
                    audioOutputManager?.ApplyRangeMultiplierToPlayer(capi.World.Player.PlayerUID, 1.0f, voiceRangeBoost);
                }

                if (!string.IsNullOrEmpty(soundEffectName))
                {
                    audioOutputManager?.ApplyEffectToPlayer(capi.World.Player.PlayerUID, soundEffectName);
                }

                isAmplifierActive = true;
            }

            handling = EnumHandHandling.PreventDefault;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!isAmplifierActive) return;

            if (byEntity.Api is ICoreClientAPI capi && byEntity == capi.World.Player.Entity)
            {
                audioOutputManager?.ResetRangeForPlayer(capi.World.Player.PlayerUID);
                audioOutputManager?.SetVoiceLevelForPlayer(capi.World.Player.PlayerUID, VoiceLevel.Talking);

                audioOutputManager?.ClearEffectForPlayer(capi.World.Player.PlayerUID);

                isAmplifierActive = false;
            }
        }
    }
}
