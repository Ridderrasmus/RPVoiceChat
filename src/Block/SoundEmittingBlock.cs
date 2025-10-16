using System;
using RPVoiceChat;
using RPVoiceChat.Audio;
using RPVoiceChat.Config;
using Vintagestory.API.Common;

public class SoundEmittingBlock : Block
{
    private Random Random = new Random();

    protected int AudibleDistance = 16;
    protected float DefaultVolume = 1f;
    private float soundDuration = 2f;

    private bool isUsable = true;
    private bool cooldownActive = false;

    public const float MaxGain = 2f;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        // Default values from JSON assets
        int defaultDistance = Attributes?["soundAudibleDistance"].AsInt(AudibleDistance) ?? AudibleDistance;
        float defaultVolume = Attributes?["soundVolume"].AsFloat(DefaultVolume) ?? DefaultVolume;
        soundDuration = Attributes?["soundDuration"].AsFloat(soundDuration) ?? soundDuration;

        // Override with server configurations if available
        AudibleDistance = GetConfiguredAudibleDistance(api, defaultDistance);
        DefaultVolume = defaultVolume;
    }

    private int GetConfiguredAudibleDistance(ICoreAPI api, int defaultDistance)
    {
        // Only on server side, use server configurations
        if (api.Side != EnumAppSide.Server) return defaultDistance;

        // Use the block class to determine the appropriate configuration
        string blockClass = GetType().Name.ToLower();
        
        switch (blockClass)
        {
            case "soundemittingblock":
                return GetSoundEmittingBlockDistance();
            default:
                return defaultDistance;
        }
    }

    private int GetSoundEmittingBlockDistance()
    {
        // Identify the specific sound block type by its code
        string blockCode = Code?.ToString()?.ToLower() ?? "";
        
        if (blockCode == "callbell")
            return ServerConfigManager.CallbellAudibleDistance;
        else if (blockCode == "carillonbell")
            return ServerConfigManager.CarillonbellAudibleDistance;
        else if (blockCode == "churchbell")
            return ServerConfigManager.ChurchbellAudibleDistance;
        
        // Default value if no match found
        return Attributes?["soundAudibleDistance"].AsInt(16) ?? 16;
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

        float rawVolume = DefaultVolume * PlayerListener.BlockGain;
        float finalVolume = Math.Clamp(rawVolume, 0f, MaxGain);

        if (player == null)
        {
            api.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/" + sound + ".ogg"),
                x, y, z,
                null,
                false,
                AudibleDistance,
                finalVolume
            );
        }
        else
        {
            api.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/" + sound + ".ogg"),
                x, y, z,
                player,
                false,
                AudibleDistance,
                finalVolume
            );
        }

        StartSoundDurationCooldown();
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
        float rawVolume = DefaultVolume * PlayerListener.BlockGain;
        float finalVolume = Math.Clamp(rawVolume, 0f, MaxGain);

        if (dualCall)
        {
            player.Entity.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/" + sound + ".ogg"),
                player,
                player,
                false,
                AudibleDistance,
                finalVolume
            );
        }
        else
        {
            player.Entity.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/" + sound + ".ogg"),
                player,
                null,
                false,
                AudibleDistance,
                finalVolume
            );
        }

        StartSoundDurationCooldown();
    }

    /// <summary>
    /// Starts a cooldown timer based on the duration of the sound to prevent overlapping playback.
    /// 
    /// Note:
    /// 1) Each sound-emitting block should define a "soundDuration" value in its JSON attributes,
    ///    representing the length of the sound in seconds.
    /// 2) This method could be improved by dynamically retrieving the actual duration of the sound
    ///    using a third-party library such as NVorbis, rather than relying on a static value.
    /// </summary>
    private void StartSoundDurationCooldown()
    {
        if (cooldownActive) return;

        cooldownActive = true;

        api.World.RegisterCallback((float dt) => {
            isUsable = true;
            cooldownActive = false;
        }, (int)(soundDuration * 1000));
    }

}