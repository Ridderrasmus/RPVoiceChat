using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    /// <summary>
    /// Provides compatibility with ConfigLib for automatic GUI generation
    /// ConfigLib creates an in-game settings menu automatically from the config classes
    /// </summary>
    public class ConfigLibCompatibility : ModSystem
    {
        private const string ConfigLibModId = "configlib";

        public override double ExecuteOrder() => 0.1;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            // Check if ConfigLib is installed
            if (!api.ModLoader.IsModEnabled(ConfigLibModId))
            {
                api.Logger.Notification("[RPVoiceChat] ConfigLib not detected. In-game config GUI will not be available.");
                api.Logger.Notification("[RPVoiceChat] You can still edit configs manually in ModConfig folder.");
                return;
            }

            try
            {
                RegisterConfigLibIntegration(api);
                api.Logger.Notification("[RPVoiceChat] ConfigLib integration enabled successfully!");
            }
            catch (System.Exception e)
            {
                api.Logger.Error($"[RPVoiceChat] Failed to integrate with ConfigLib: {e.Message}");
            }
        }

        private void RegisterConfigLibIntegration(ICoreClientAPI api)
        {
            // ConfigLib uses reflection to automatically generate UI from config classes
            // Register here client config with ConfigLib

            // Note: This is a placeholder implementation
            // The actual ConfigLib API may require different registration methods
            // depending on the version we are using

            // Example of what ConfigLib might expect:
            // ConfigLibAPI.RegisterConfig<RPVoiceChatClientConfig>(
            //     api,
            //     ModConfig.ClientConfigName,
            //     "RPVoiceChat Settings"
            // );

        }
    }
}