using RPVoiceChat.Audio;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public abstract class ConfigDialog : GuiDialog
    {
        protected MicrophoneManager _audioInputManager;
        protected AudioOutputManager _audioOutputManager;
        private bool _isSetup;
        private List<ConfigOption> ConfigOptions = new List<ConfigOption>();
        private List<GuiTab> ConfigTabs = new List<GuiTab>();
        private GuiElementAudioMeter AudioMeter;
        private long audioMeterId;

        protected class ConfigOption
        {
            public ActionConsumable AdvancedAction;
            public string SwitchKey;
            public string SliderKey;
            public string SpecialSliderKey;
            public string DropdownKey;
            public string Text;
            public string Tooltip;
            public bool InstantSlider;
            public string[] DropdownValues { get; internal set; }
            public string[] DropdownNames { get; internal set; }
            public GuiTab Tab;
            public Action<bool> ToggleAction;
            public ActionConsumable<int> SlideAction;
            public SelectionChangedDelegate DropdownSelect { get; internal set; }
        }


        public ConfigDialog(ICoreClientAPI capi) : base(capi)
        {

        }

        protected void RegisterTab(GuiTab tab)
        {
            ConfigTabs.Add(tab);
        }

        protected void RegisterOption(ConfigOption option)
        {
            ConfigOptions.Add(option);
        }

        protected void SetupDialog()
        {
            _isSetup = true;

            var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            const int switchSize = 20;
            const int switchPadding = 10;
            const double sliderWidth = 200.0;
            var font = CairoFont.WhiteSmallText();

            var tabBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight + 5, 105, 300).WithAlignment(EnumDialogArea.LeftTop);
            var textBounds = ElementBounds.Fixed(tabBounds.fixedWidth + 20, GuiStyle.TitleBarHeight, 210, switchSize);
            var switchBounds = ElementBounds.Fixed(textBounds.fixedWidth + textBounds.fixedX + 40, GuiStyle.TitleBarHeight, 300, switchSize);

            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var composer = capi.Gui.CreateCompo("rpvcconfigmenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("RP VC Config Menu", OnTitleBarCloseClicked)
                .BeginChildElements(bgBounds)
                .AddVerticalTabs(ConfigTabs.ToArray(), tabBounds, OnTabClicked, "configTabs");

            var activeTabIndex = ClientSettings.GetInt("activeConfigTab", 0);
            var activeTab = ConfigTabs[activeTabIndex];
            foreach (ConfigOption option in ConfigOptions)
            {
                if (option.Tab != activeTab) continue;

                composer.AddStaticText(option.Text, font, textBounds);
                if (option.Tooltip != null)
                {
                    composer.AddHoverText(option.Tooltip, font, 260, textBounds);
                }

                if (option.SliderKey != null)
                {
                    composer.AddSlider(option.SlideAction, switchBounds.FlatCopy().WithFixedWidth(sliderWidth),
                        option.SliderKey);
                }
                else if (option.SwitchKey != null)
                {
                    composer.AddSwitch(option.ToggleAction, switchBounds, option.SwitchKey, switchSize);
                }
                else if (option.DropdownKey != null)
                {
                    switchBounds.fixedWidth = 200;
                    composer.AddDropDown(option.DropdownValues, option.DropdownNames, 0, option.DropdownSelect, switchBounds, option.DropdownKey);
                }
                else if (option.SpecialSliderKey != null)
                {
                    AudioMeter = new GuiElementAudioMeter(capi, switchBounds.FlatCopy().WithFixedWidth(sliderWidth));
                    AudioMeter.SetCoefficient((100 / _audioInputManager.GetMaxInputThreshold()));
                    composer.AddInteractiveElement(AudioMeter, option.SpecialSliderKey);
                }

                textBounds = textBounds.BelowCopy(fixedDeltaY: switchPadding);
                switchBounds = switchBounds.BelowCopy(fixedDeltaY: switchPadding);
            }

            SingleComposer = composer.EndChildElements().Compose();
        }

        public override bool TryOpen()
        {
            if (!_isSetup)
                SetupDialog();

            var success = base.TryOpen();
            if (!success)
                return false;

            RefreshValues();
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }

        private void OnTabClicked(int dataInt, GuiTab _)
        {
            ClientSettings.Set("activeConfigTab", dataInt);
            SetupDialog();
            RefreshValues();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            capi.Event.UnregisterGameTickListener(audioMeterId);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            audioMeterId = capi.Event.RegisterGameTickListener(TickUpdate, 20);

        }

        private void TickUpdate(float obj)
        {
            AudioMeter?.SetThreshold(_audioInputManager.GetInputThreshold());
            var amplitude = Math.Max(_audioInputManager.Amplitude, _audioInputManager.AmplitudeAverage);
            if (ModConfig.Config.IsMuted) amplitude = 0;
            AudioMeter?.UpdateVisuals(amplitude);
        }

        protected abstract void RefreshValues();

        public override string ToggleKeyCombinationCode => null;
    }

    public class MainConfig : ConfigDialog
    {
        RPVoiceChatConfig _config;

        public MainConfig(ICoreClientAPI capi, MicrophoneManager audioInputManager, AudioOutputManager audioOutputManager) : base(capi)
        {
            _config = ModConfig.Config;
            _audioInputManager = audioInputManager;
            _audioOutputManager = audioOutputManager;

            var audioInputTab = new GuiTab() { Name = "Audio Input" };
            var audioOutputTab = new GuiTab() { Name = "Audio Output" };
            var effectsTab = new GuiTab() { Name = "Effects" };
            var interfaceTab = new GuiTab() { Name = "Interface" };
            RegisterTab(audioInputTab);
            RegisterTab(audioOutputTab);
            RegisterTab(effectsTab);
            RegisterTab(interfaceTab);

            RegisterOption(new ConfigOption
            {
                Text = "Input Device",
                DropdownKey = "inputDevice",
                Tooltip = "Input device",
                Tab = audioInputTab,
                DropdownNames = _audioInputManager.GetInputDeviceNames(),
                DropdownValues = _audioInputManager.GetInputDeviceNames(),
                DropdownSelect = OnChangeInputDevice
            });

            RegisterOption(new ConfigOption
            {
                Text = "Push To Talk",
                SwitchKey = "togglePushToTalk",
                Tooltip = "Use push to talk instead of voice activation",
                Tab = audioInputTab,
                ToggleAction = OnTogglePushToTalk
            });

            RegisterOption(new ConfigOption
            {
                Text = "Mute",
                SwitchKey = "muteMicrophone",
                Tooltip = "Mute your microphone",
                Tab = audioInputTab,
                ToggleAction = OnToggleMuted
            });

            RegisterOption(new ConfigOption
            {
                Text = "Loopback",
                SwitchKey = "loopback",
                Tooltip = "Play recorded audio through output audio",
                Tab = audioOutputTab,
                ToggleAction = OnToggleLoopback
            });

            RegisterOption(new ConfigOption
            {
                Text = "Players Volume Level",
                SliderKey = "outputGain",
                Tooltip = "How much to increase volume of other players",
                Tab = audioOutputTab,
                SlideAction = SlideOutputGain
            });

            RegisterOption(new ConfigOption
            {
                Text = "Recording Volume",
                SliderKey = "inputGain",
                Tooltip = "Changes your own volume",
                Tab = audioInputTab,
                SlideAction = SlideInputGain
            });

            RegisterOption(new ConfigOption
            {
                Text = "Audio Input Threshold",
                SliderKey = "inputThreshold",
                Tooltip = "At which threshold your audio starts transmitting",
                Tab = audioInputTab,
                SlideAction = SlideInputThreshold
            });

            RegisterOption(new ConfigOption
            {
                Text = "Audio Meter",
                SpecialSliderKey = "audioMeter",
                Tooltip = "Shows your audio amplitude",
                Tab = audioInputTab
            });

            RegisterOption(new ConfigOption
            {
                Text = "Toggle HUD",
                SwitchKey = "toggleHUD",
                Tooltip = "Toggle visibility of HUD elements",
                Tab = interfaceTab,
                ToggleAction = OnToggleHUD
            });

            RegisterOption(new ConfigOption
            {
                Text = "Muffling",
                SwitchKey = "toggleMuffling",
                Tooltip = "Muffle audio when other players are behind solid obstacles",
                Tab = effectsTab,
                ToggleAction = OnToggleMuffling
            });

            RegisterOption(new ConfigOption
            {
                Text = "Denoising",
                SwitchKey = "toggleDenoising",
                Tooltip = "Enable denoising of your audio",
                Tab = effectsTab,
                ToggleAction = OnToggleDenoising
            });

            RegisterOption(new ConfigOption
            {
                Text = "Background noise detection",
                SliderKey = "denoisingSensitivity",
                Tooltip = "Sets sensitivity for background noise. Audio detected as noise will be denoised with max strength.",
                Tab = effectsTab,
                SlideAction = SlideDenoisingSensitivity
            });

            RegisterOption(new ConfigOption
            {
                Text = "Denoising strength",
                SliderKey = "denoisingStrength",
                Tooltip = "Sets intensity of denosing for audio detected as voice. Lower it if your voice is too distorted.",
                Tab = effectsTab,
                SlideAction = SlideDenoisingStrength
            });
        }

        protected override void RefreshValues()
        {
            if (!IsOpened())
                return;

            SetValue("configTabs", ClientSettings.GetInt("activeConfigTab", 0));
            SetValue("inputDevice", _config.CurrentInputDevice ?? "Default");
            SetValue("togglePushToTalk", _config.PushToTalkEnabled);
            SetValue("muteMicrophone", _config.IsMuted);
            SetValue("loopback", _config.IsLoopbackEnabled);
            SetValue("outputGain", new dynamic[] { _config.OutputGain, 0, 200, 1, "%" });
            SetValue("inputGain", new dynamic[] { _config.InputGain, 0, 100, 1, "%" });
            SetValue("inputThreshold", new dynamic[] { _config.InputThreshold, 0, 100, 1, "" });
            SetValue("toggleHUD", _config.IsHUDShown);
            SetValue("toggleMuffling", ClientSettings.GetBool("muffling", true));
            SetValue("toggleDenoising", _config.IsDenoisingEnabled);
            SetValue("denoisingSensitivity", new dynamic[] { _config.BackgroungNoiseThreshold, 0, 100, 1, "%" });
            SetValue("denoisingStrength", new dynamic[] { _config.VoiceDenoisingStrength, 0, 100, 1, "%" });
        }

        protected void SetValue(string key, dynamic value)
        {
            GuiElement element = SingleComposer.GetElement(key);
            if (element is null) return;
            else if (element is GuiElementDropDown) ((GuiElementDropDown)element).SetSelectedValue(value);
            else if (element is GuiElementSwitch) ((GuiElementSwitch)element).On = value;
            else if (element is GuiElementSlider) ((GuiElementSlider)element).SetValues(value[0], value[1], value[2], value[3], value[4]);
            else if (element is GuiElementVerticalTabs) ((GuiElementVerticalTabs)element).activeElement = value;
            else throw new Exception("Unknown element type");
        }

        private void OnChangeInputDevice(string value, bool selected)
        {
            _audioInputManager.SetInputDevice(value);
        }

        private void OnTogglePushToTalk(bool enabled)
        {
            _config.PushToTalkEnabled = enabled;
            ModConfig.Save(capi);
        }

        private void OnToggleMuted(bool enabled)
        {
            _config.IsMuted = enabled;
            ModConfig.Save(capi);
        }

        protected void OnToggleLoopback(bool enabled)
        {
            _config.IsLoopbackEnabled = enabled;
            ModConfig.Save(capi);
            _audioOutputManager.IsLoopbackEnabled = enabled;
        }

        private bool SlideOutputGain(int gain)
        {
            _config.OutputGain = gain;
            ModConfig.Save(capi);

            return true;
        }

        private bool SlideInputGain(int gain)
        {
            _config.InputGain = gain;
            ModConfig.Save(capi);
            _audioInputManager.SetGain(gain);

            return true;
        }

        private bool SlideInputThreshold(int threshold)
        {
            _config.InputThreshold = threshold;
            _audioInputManager.SetThreshold(threshold);
            ModConfig.Save(capi);

            return true;
        }

        private void OnToggleHUD(bool enabled)
        {
            _config.IsHUDShown = enabled;
            ModConfig.Save(capi);
        }

        private void OnToggleMuffling(bool enabled)
        {
            ClientSettings.Set("muffling", enabled);
        }

        private void OnToggleDenoising(bool enabled)
        {
            _config.IsDenoisingEnabled = enabled;
            ModConfig.Save(capi);
        }

        private bool SlideDenoisingSensitivity(int sensitivity)
        {
            _config.BackgroungNoiseThreshold = sensitivity;
            _audioInputManager.SetDenoisingSensitivity(sensitivity);
            ModConfig.Save(capi);

            return true;
        }

        private bool SlideDenoisingStrength(int strength)
        {
            _config.VoiceDenoisingStrength = strength;
            _audioInputManager.SetDenoisingStrength(strength);
            ModConfig.Save(capi);

            return true;
        }
    }
}
