using RPVoiceChat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

public class VoiceAmplifierItem : Item
{
    private string soundEffectName = null;
    private bool isAmplifierActive = false;
    private VoiceLevel originalVoiceLevel;
    private int originalTransmissionRange;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        var attr = Attributes;
        if (attr != null && attr["applySoundEffect"]?.Exists == true)
        {
            soundEffectName = attr["applySoundEffect"].AsString(null);
        }
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!firstEvent || isAmplifierActive)
        {
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        if (byEntity.Api is ICoreClientAPI capi && byEntity == capi.World.Player.Entity)
        {
            var microphoneManager = RPVoiceChatClient.MicrophoneManagerInstance;
            if (microphoneManager != null)
            {
                // Save original state
                originalVoiceLevel = microphoneManager.GetVoiceLevel();
                originalTransmissionRange = microphoneManager.GetTransmissionRange();

                var attr = slot?.Itemstack?.Collectible?.Attributes;

                // Set voice level (audio quality)
                microphoneManager.SetVoiceLevel(VoiceLevel.Shouting);

                // Set transmission range (distance)
                if (attr != null && attr["voiceRangeBlocks"]?.Exists == true)
                {
                    int rangeBlocks = attr["voiceRangeBlocks"].AsInt(50);
                    microphoneManager.SetTransmissionRange(rangeBlocks);
                }

                isAmplifierActive = true;
            }

            // Apply sound effect
            if (!string.IsNullOrEmpty(soundEffectName))
            {
                var audioOutputManager = RPVoiceChatClient.AudioOutputManagerInstance;
                string playerUID = capi.World.Player.PlayerUID;
                audioOutputManager?.ApplyEffectToPlayer(playerUID, soundEffectName);
            }
        }

        handling = EnumHandHandling.PreventDefault;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (!isAmplifierActive) return;

        if (byEntity.Api is ICoreClientAPI capi && byEntity == capi.World.Player.Entity)
        {
            var microphoneManager = RPVoiceChatClient.MicrophoneManagerInstance;
            if (microphoneManager != null)
            {
                // Restore original state
                microphoneManager.SetVoiceLevel(originalVoiceLevel);
                microphoneManager.SetTransmissionRange(originalTransmissionRange);
            }

            // Clear sound effect
            if (!string.IsNullOrEmpty(soundEffectName))
            {
                var audioOutputManager = RPVoiceChatClient.AudioOutputManagerInstance;
                string playerUID = capi.World.Player.PlayerUID;
                audioOutputManager?.ClearEffectForPlayer(playerUID);
            }

            isAmplifierActive = false;
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        // Continue interaction as long as the amplifier is active
        return isAmplifierActive;
    }

    public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
    {
        // If amplifier is active but interaction is no longer maintained, deactivate
        if (isAmplifierActive)
        {
            OnHeldInteractStop(0f, slot, byEntity, null, null);
        }
    }
}