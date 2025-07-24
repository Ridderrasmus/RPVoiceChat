using Vintagestory.API.Client;
using RPVoiceChat.DB;
using RPVoiceChat.API;

namespace RPVoiceChat.Audio.Sources
{
    public class PlayerVoiceSourceProvider : IVoiceSourceProvider
    {
        private readonly ICoreClientAPI capi;
        private readonly ClientSettingsRepository settings;

        public PlayerVoiceSourceProvider(ICoreClientAPI capi, ClientSettingsRepository settings)
        {
            this.capi = capi;
            this.settings = settings;
        }

        public IVoiceSource CreateVoiceSource(string sourceId)
        {
            var player = capi.World.PlayerByUid(sourceId);
            if (player != null)
            {
                return new PlayerVoiceSource(player, capi, settings);
            }
            return null;
        }
    }

    public class LocalPlayerVoiceSourceProvider : IVoiceSourceProvider
    {
        private readonly ICoreClientAPI capi;
        private readonly ClientSettingsRepository settings;

        public LocalPlayerVoiceSourceProvider(ICoreClientAPI capi, ClientSettingsRepository settings)
        {
            this.capi = capi;
            this.settings = settings;
        }

        public IVoiceSource CreateVoiceSource(string sourceId)
        {
            var player = capi.World.Player;
            if (player != null && player.PlayerUID == sourceId)
            {
                PlayerVoiceSource voiceSource = new PlayerVoiceSource(player, capi, settings)
                {
                    IsLocational = false // Local player voice source is not locational
                };

                voiceSource.Start(); // Start immediately for local player

                return voiceSource;
            }
            return null;
        }

    }
}
