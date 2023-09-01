using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    static class WorldConfig
    {
        private static Dictionary<VoiceLevel, string> configKeyByVoiceLevel = new Dictionary<VoiceLevel, string>
        {
            { VoiceLevel.Whispering, "rpvoicechat:distance-whisper" },
            { VoiceLevel.Talking, "rpvoicechat:distance-talk" },
            { VoiceLevel.Shouting, "rpvoicechat:distance-shout" },
        };
        private static string defaultConfigKey = configKeyByVoiceLevel[VoiceLevel.Talking];

        public static int GetVoiceDistance(ICoreAPI api, VoiceLevel voiceLevel)
        {
            string configKey;
            configKeyByVoiceLevel.TryGetValue(voiceLevel, out configKey);
            configKey = configKey ?? defaultConfigKey;

            int voiceDistance = api.World.Config.GetInt(configKey);

            return voiceDistance;
        }
    }
}
