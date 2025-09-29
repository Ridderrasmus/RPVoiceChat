using RPVoiceChat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

public class VoiceAmplifierItem : Item
{
    private string soundEffectName = null;
    private bool isAmplifierActive = false;
    private VoiceLevel originalVoiceLevel;
    private int originalTransmissionRange;
    private bool ignoreDistanceReduction = false;
    private float wallThicknessOverride = -1f;
    private bool globalBroadcast = false;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        var attr = Attributes;
        if (attr != null)
        {
            soundEffectName = attr["applySoundEffect"]?.AsString(null);
            ignoreDistanceReduction = attr["ignoreDistanceReduction"]?.AsBool(false) ?? false;
            wallThicknessOverride = attr["wallThicknessOverride"]?.AsFloat(-1f) ?? -1f;
            globalBroadcast = attr["globalBroadcast"]?.AsBool(false) ?? false;
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
            var audioOutputManager = RPVoiceChatClient.AudioOutputManagerInstance;

            if (microphoneManager != null)
            {
                originalVoiceLevel = microphoneManager.GetVoiceLevel();
                originalTransmissionRange = microphoneManager.GetTransmissionRange();

                var attr = slot?.Itemstack?.Collectible?.Attributes;

                microphoneManager.SetVoiceLevel(VoiceLevel.Shouting);

                if (attr != null && attr["voiceRangeBlocks"]?.Exists == true)
                {
                    int rangeBlocks = attr["voiceRangeBlocks"].AsInt(50);
                    microphoneManager.SetTransmissionRange(rangeBlocks);
                }

                if (ignoreDistanceReduction)
                {
                    microphoneManager.SetIgnoreDistanceReduction(true);
                }

                if (wallThicknessOverride >= 0f)
                {
                    microphoneManager.SetWallThicknessOverride(wallThicknessOverride);
                }

                if (globalBroadcast)
                {
                    microphoneManager.SetGlobalBroadcast(true);
                }

                isAmplifierActive = true;
            }

            if (!string.IsNullOrEmpty(soundEffectName) && audioOutputManager != null)
            {
                string playerUID = capi.World.Player.PlayerUID;
                audioOutputManager.ApplyEffectToPlayer(playerUID, soundEffectName);
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
            var audioOutputManager = RPVoiceChatClient.AudioOutputManagerInstance;

            if (microphoneManager != null)
            {
                microphoneManager.SetVoiceLevel(originalVoiceLevel);
                microphoneManager.SetTransmissionRange(originalTransmissionRange);
                microphoneManager.SetIgnoreDistanceReduction(false);
                microphoneManager.ResetWallThicknessOverride();
                microphoneManager.ResetGlobalBroadcast();
            }

            if (!string.IsNullOrEmpty(soundEffectName) && audioOutputManager != null)
            {
                string playerUID = capi.World.Player.PlayerUID;
                audioOutputManager.ClearEffectForPlayer(playerUID);
            }

            isAmplifierActive = false;
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        return isAmplifierActive;
    }

    public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
    {
        if (isAmplifierActive)
        {
            OnHeldInteractStop(0f, slot, byEntity, null, null);
        }
    }
}