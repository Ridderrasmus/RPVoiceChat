using RPVoiceChat.Audio;
using RPVoiceChat.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    public abstract class ConfigDialog : GuiDialog
    {
        private bool _isSetup;
        private List<ConfigOption> ConfigOptions = new List<ConfigOption>();
        private List<ConfigTab> ConfigTabs = new List<ConfigTab>();

        protected class ConfigTab : GuiTab
        {
            public CairoFont Font = CairoFont.WhiteDetailText().WithFontSize(17f);
            public double TextWidth { get => _TextWidth(); }

            private double _TextWidth()
            {
                if (Name == null || Name == "") return 0;
                return Font.GetTextExtents(Name).Width;
            }
        }

        protected class ConfigOption
        {
            public ActionConsumable AdvancedAction;
            public string SwitchKey;
            public string SliderKey;
            public string InteractiveElementKey;
            public string DropdownKey;
            public string Text;
            public string Tooltip;
            public bool InstantSlider;
            public string[] DropdownValues { get; internal set; }
            public string[] DropdownNames { get; internal set; }
            public GuiTab Tab;
            public IExtendedGuiElement InteractiveElement;
            public Action<bool> ToggleAction;
            public ActionConsumable<int> SlideAction;
            public SelectionChangedDelegate DropdownSelect { get; internal set; }
            public CairoFont Font = CairoFont.WhiteSmallText();
            public double TextWidth { get => _TextWidth(); }

            private double _TextWidth()
            {
                if (Text == null || Text == "") return 0;
                return Font.GetTextExtents(Text).Width;
            }
        }


        public ConfigDialog(ICoreClientAPI capi) : base(capi) { }

        protected void RegisterTab(ConfigTab tab)
        {
            ConfigTabs.Add(tab);
        }

        protected void RegisterOption(ConfigOption option)
        {
            ConfigOptions.Add(option);
        }

        protected void SetupDialog()
        {
            const int tabsTopPadding = 5;
            const int textLeftPadding = 20;
            const int settingsLeftPadding = 40;
            const int settingDeltaY = 10;
            const int settingHeight = 20;
            const int switchSize = 20;
            const double sliderWidth = 200.0;
            const int tooltipWidth = 260;
            const int _tabTextPadding = 4;
            _isSetup = true;

            var activeTabIndex = ClientSettings.GetInt("activeConfigTab", 0);
            var activeTab = ConfigTabs[activeTabIndex];
            var displayedOptions = ConfigOptions.FindAll(e => e.Tab == activeTab);
            double maxTextWidth = displayedOptions.DefaultIfEmpty().Max(e => e?.TextWidth ?? 0) + 2;
            double maxTabWidth = ConfigTabs.DefaultIfEmpty().Max(e => e?.TextWidth ?? 0) + _tabTextPadding * 2;

            var tabsBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight + tabsTopPadding, maxTabWidth, 300).WithAlignment(EnumDialogArea.LeftTop);
            var textBounds = ElementBounds.Fixed(tabsBounds.fixedWidth + textLeftPadding, GuiStyle.TitleBarHeight, maxTextWidth, settingHeight);
            var settingBounds = ElementBounds.Fixed(textBounds.fixedWidth + textBounds.fixedX + settingsLeftPadding, GuiStyle.TitleBarHeight, 300, settingHeight);
            var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var composer = capi.Gui.CreateCompo("rpvcconfigmenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("RP VC Config Menu", OnTitleBarCloseClicked)
                .BeginChildElements(bgBounds)
                .AddVerticalTabs(ConfigTabs.ToArray(), tabsBounds, OnTabClicked, "configTabs");

            foreach (ConfigOption option in displayedOptions)
            {
                var wideSettingBounds = settingBounds.FlatCopy().WithFixedWidth(sliderWidth);
                if (option.Text != null)
                {
                    composer.AddStaticText(option.Text, option.Font, textBounds);
                }
                if (option.Tooltip != null)
                {
                    composer.AddHoverText(option.Tooltip, option.Font, tooltipWidth, textBounds);
                }

                if (option.SliderKey != null)
                {
                    composer.AddSlider(option.SlideAction, wideSettingBounds, option.SliderKey);
                }
                else if (option.SwitchKey != null)
                {
                    composer.AddSwitch(option.ToggleAction, settingBounds, option.SwitchKey, switchSize);
                }
                else if (option.DropdownKey != null)
                {
                    composer.AddDropDown(
                        option.DropdownValues,
                        option.DropdownNames,
                        0,
                        option.DropdownSelect,
                        wideSettingBounds,
                        option.DropdownKey
                    );
                }
                else if (option.InteractiveElementKey != null)
                {
                    IExtendedGuiElement element = option.InteractiveElement;
                    element.SetKey(option.InteractiveElementKey);
                    element.SetBounds(option.Text == null ? textBounds : settingBounds);
                    composer.AddInteractiveElement((GuiElement)element, option.InteractiveElementKey);
                }

                textBounds = textBounds.BelowCopy(fixedDeltaY: settingDeltaY);
                settingBounds = settingBounds.BelowCopy(fixedDeltaY: settingDeltaY);
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

        protected abstract void RefreshValues();

        public override string ToggleKeyCombinationCode => null;
    }

    public class ModMenuDialog : ConfigDialog
    {
        private RPVoiceChatConfig _config;
        private MicrophoneManager _audioInputManager;
        private AudioOutputManager _audioOutputManager;

        public ModMenuDialog(ICoreClientAPI capi, MicrophoneManager audioInputManager, AudioOutputManager audioOutputManager, ClientSettingsRepository settingsRepository) : base(capi)
        {
            _config = ModConfig.Config;
            _audioInputManager = audioInputManager;
            _audioOutputManager = audioOutputManager;

            var audioInputTab = new ConfigTab() { Name = "Audio Input" };
            var audioOutputTab = new ConfigTab() { Name = "Audio Output" };
            var effectsTab = new ConfigTab() { Name = "Effects" };
            var interfaceTab = new ConfigTab() { Name = "Interface" };
            var playerListTab = new ConfigTab() { Name = "Player List" };
            RegisterTab(audioInputTab);
            RegisterTab(audioOutputTab);
            RegisterTab(effectsTab);
            RegisterTab(interfaceTab);
            RegisterTab(playerListTab);

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
                InteractiveElementKey = "audioMeter",
                Tooltip = "Shows your audio amplitude",
                Tab = audioInputTab,
                InteractiveElement = new AudioMeter(capi, _audioInputManager, this)
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

            RegisterOption(new ConfigOption
            {
                InteractiveElementKey = "playerList",
                Tab = playerListTab,
                InteractiveElement = new PlayerList(capi, settingsRepository, this)
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
            SetValue("playerList", null);
        }

        protected void SetValue(string key, dynamic value)
        {
            GuiElement element = SingleComposer.GetElement(key);
            if (element is null) return;
            else if (element is GuiElementDropDown dropDown) dropDown.SetSelectedValue(value);
            else if (element is GuiElementSwitch switchBox) switchBox.On = value;
            else if (element is GuiElementSlider slider) slider.SetValues(value[0], value[1], value[2], value[3], value[4]);
            else if (element is GuiElementVerticalTabs verticalTabs) verticalTabs.activeElement = value;
            else if (element is PlayerList playerList) playerList.SetupElement();
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
