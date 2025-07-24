using RPVoiceChat.API;
using RPVoiceChat.Audio;
using RPVoiceChat.DB;
using RPVoiceChat.VoiceGroups.Manager;
using System;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class GuiManager : IDisposable
    {
        public AudioWizardDialog audioWizardDialog { get; }
        public FirstLaunchDialog firstLaunchDialog { get; }
        public ModMenuDialog modMenuDialog { get; }
        public GroupDisplay groupDisplay { get; }

        public GuiManager(ICoreClientAPI capi, MicrophoneManager audioInputManager, ClientSettingsRepository settingsRepository)
        {
            audioWizardDialog = new AudioWizardDialog(capi, audioInputManager, VoiceChatSystem.Instance);
            firstLaunchDialog = new FirstLaunchDialog(capi, this);
            modMenuDialog = new ModMenuDialog(capi, audioInputManager, VoiceChatSystem.Instance, settingsRepository, this);
            
            // Register main dialogs
            capi.Gui.RegisterDialog(new SpeechIndicator(capi, audioInputManager));
            capi.Gui.RegisterDialog(new VoiceLevelIcon(capi, audioInputManager));
            
            // Initialize voice group manager and group display
            var voiceGroupManager = new VoiceGroupManagerClient(capi);
            var audioOutputManager = VoiceChatSystem.Instance.GetAudioOutputManager();
            
            if (audioOutputManager != null)
            {
                groupDisplay = new GroupDisplay(capi, voiceGroupManager, audioOutputManager);
                capi.Gui.RegisterDialog(groupDisplay);
            }
            
            // Initialize player name tag renderer
            new PlayerNameTagRenderer(capi, VoiceChatSystem.Instance);
        }

        public void Dispose()
        {
            audioWizardDialog?.Dispose();
            firstLaunchDialog?.Dispose();
            modMenuDialog?.Dispose();
            groupDisplay?.Dispose();
        }
    }
}
