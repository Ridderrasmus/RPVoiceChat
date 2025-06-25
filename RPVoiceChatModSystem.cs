using Vintagestory.API.Client;
using Vintagestory.API.Common;
using RPVoiceChat.Audio;

namespace RPVoiceChat
{
    public class RPVoiceChatModSystem : ModSystem
    {
        public static AudioSettings AudioSettings;
        public static AudioSourceManager AudioSourceManager;

        public override void Start(ICoreAPI api)
        {
            api.Logger.Notification("RPVoiceChat mod starting [" + api.Side + "]");
            AudioSettings = api.LoadModConfig<AudioSettings>("rpvoicechat:config/AudioConfig.json")
                             ?? new AudioSettings();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            AudioSourceManager = new AudioSourceManager(api);
        }
    }
}