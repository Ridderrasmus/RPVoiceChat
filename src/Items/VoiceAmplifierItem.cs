using System;
using RPVoiceChat.Audio;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Items
{
    public class VoiceAmplifierItem : Item
    {
        public AudioOutputManager audioOutputManager;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.Api is ICoreClientAPI capi && byEntity == capi.World.Player.Entity)
            {
                VoiceLevel voiceLevel = VoiceLevel.Talking;

                var attr = slot?.Itemstack?.Collectible?.Attributes;
                if (attr != null && attr["voiceLevel"]?.Exists == true)
                {
                    string voiceLevelStr = attr["voiceLevel"].AsString("Talking");

                    if (Enum.TryParse(voiceLevelStr, ignoreCase: true, out VoiceLevel parsedLevel))
                    {
                        voiceLevel = parsedLevel;
                    }
                }

                audioOutputManager?.SetVoiceLevelForPlayer(capi.World.Player.PlayerUID, voiceLevel);
            }

            handling = EnumHandHandling.PreventDefault;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Api is ICoreClientAPI capi && byEntity == capi.World.Player.Entity)
            {
                audioOutputManager?.ResetRangeForPlayer(capi.World.Player.PlayerUID);
                audioOutputManager?.SetVoiceLevelForPlayer(capi.World.Player.PlayerUID, VoiceLevel.Talking);
            }
        }
    }
}
