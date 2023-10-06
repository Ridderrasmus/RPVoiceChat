using RPVoiceChat.Utils;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        protected RPVoiceChatConfig config;
        public static readonly string modID = "rpvoicechat";

        public override void StartPre(ICoreAPI api)
        {
            ModConfig.ReadConfig(api);
            config = ModConfig.Config;
            new Logger(api);
            EmbeddedDllClass.ExtractEmbeddedDlls();
            EmbeddedDllClass.LoadDll("OpenTK.Mathematics.dll");
            EmbeddedDllClass.LoadDll("OpenTK.Audio.OpenAL.dll");
        }

        public override void Start(ICoreAPI api)
        {
            ItemRegistry.RegisterItems(api);
            BlockRegistry.RegisterBlocks(api);
        }
    }
}
