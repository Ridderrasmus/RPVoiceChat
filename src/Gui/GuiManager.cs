using RPVoiceChat.Audio;
using RPVoiceChat.DB;
using System;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class GuiManager : IDisposable
    {
        public AudioWizardDialog audioWizardDialog { get; }
        public FirstLaunchDialog firstLaunchDialog { get; }
        public ModMenuDialog modMenuDialog { get; }

        public GuiManager(ICoreClientAPI capi, MicrophoneManager audioInputManager, AudioOutputManager audioOutputManager, ClientSettingsRepository settingsRepository)
        {
            audioWizardDialog = new AudioWizardDialog(capi, audioInputManager, audioOutputManager);
            firstLaunchDialog = new FirstLaunchDialog(capi, this);
            modMenuDialog = new ModMenuDialog(capi, audioInputManager, audioOutputManager, settingsRepository, this);
            capi.Gui.RegisterDialog(new SpeechIndicator(capi, audioInputManager));
            capi.Gui.RegisterDialog(new VoiceLevelIcon(capi, audioInputManager));
            new PlayerNameTagRenderer(capi, audioOutputManager);
        }

        public void Dispose()
        {
            audioWizardDialog?.Dispose();
            firstLaunchDialog?.Dispose();
            modMenuDialog?.Dispose();
        }
    }
}
