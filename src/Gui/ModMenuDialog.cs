using RPVoiceChat.Audio;
using RPVoiceChat.DB;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Gui
{
    public abstract class ConfigDialog : GuiDialog
    {
        protected const string i18nPrefix = "Gui.ModMenu";
        private const int tabsHeight = 300;
        private const int tabsTopPadding = 5;
        private const int textLeftPadding = 20;
        private const int settingsLeftPadding = 40;
        private const int settingDeltaY = 10;
        private const int settingHeight = 20;
        private const int switchSize = 20;
        private const double sliderWidth = 200.0;
        private const int tooltipWidth = 260;
        private const int _tabTextPadding = 4;
        private bool isSetup;
        private List<ConfigOption> ConfigOptions = new List<ConfigOption>();
        private List<ConfigTab> ConfigTabs = new List<ConfigTab>();

        protected class ConfigTab : GuiTab
        {
            public CairoFont Font = CairoFont.WhiteDetailText().WithFontSize(17f);
            public double TextWidth { get => _TextWidth(); }

            public ConfigTab(string i18nTabKey) : base()
            {
                Name = UIUtils.I18n($"{i18nPrefix}.Tab.{i18nTabKey}");
            }

            private double _TextWidth()
            {
                if (Name == null || Name == "") return 0;
                return Font.GetTextExtents(Name).Width + _tabTextPadding * 2;
            }
        }

        protected class ConfigOption
        {
            public bool Enabled = true;
            public string Key;
            public ElementType Type;
            public bool Label;
            public bool Tooltip;
            public GuiTab Tab;
            public IExtendedGuiElement CustomElement;
            public Action<bool> ToggleAction;
            public ActionConsumable<int> SlideAction;
            public SliderTooltipDelegate SlideTooltip;
            public string[] DropdownValues { get; internal set; }
            public string[] DropdownNames { get; internal set; }
            public SelectionChangedDelegate DropdownSelect { get; internal set; }
            public CairoFont Font = CairoFont.WhiteSmallText();
            public string Text { get => UIUtils.I18n($"{i18nPrefix}.{Key}.Label"); }
            public string TooltipText { get => UIUtils.I18n($"{i18nPrefix}.{Key}.Tooltip"); }
            public double TextWidth { get => _TextWidth(); }

            private double _TextWidth()
            {
                if (!Label || Text == null || Text == "") return 0;
                return Font.GetTextExtents(Text).Width + 2;
            }
        }

        protected enum ElementType
        {
            Slider,
            Switch,
            Dropdown,
            Custom
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
            isSetup = true;

            var activeTab = ConfigTabs[ClientSettings.ActiveConfigTab];
            var displayedOptions = ConfigOptions.FindAll(e => e.Tab == activeTab && e.Enabled);
            double maxTextWidth = displayedOptions.DefaultIfEmpty().Max(e => e?.TextWidth ?? 0);
            double maxTabWidth = ConfigTabs.DefaultIfEmpty().Max(e => e?.TextWidth ?? 0);

            var tabsBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight + tabsTopPadding, maxTabWidth, tabsHeight).WithAlignment(EnumDialogArea.LeftTop);
            var textBounds = ElementBounds.Fixed(tabsBounds.fixedWidth + textLeftPadding, GuiStyle.TitleBarHeight, maxTextWidth, settingHeight);
            var settingBounds = ElementBounds.Fixed(textBounds.fixedWidth + textBounds.fixedX + settingsLeftPadding, GuiStyle.TitleBarHeight, 0, settingHeight);
            var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var composer = capi.Gui.CreateCompo("rpvcconfigmenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n($"{i18nPrefix}.TitleBar"), OnTitleBarCloseClicked)
                .BeginChildElements(bgBounds)
                .AddVerticalTabs(ConfigTabs.ToArray(), tabsBounds, OnTabClicked, "configTabs");

            foreach (ConfigOption option in displayedOptions)
            {
                var wideSettingBounds = settingBounds.FlatCopy().WithFixedWidth(sliderWidth);

                if (option.Label) composer.AddStaticText(option.Text, option.Font, textBounds);
                if (option.Tooltip) composer.AddHoverText(option.TooltipText, option.Font, tooltipWidth, textBounds);

                switch (option.Type)
                {
                    case ElementType.Slider:
                        var slider = new GuiElementSlider(capi, option.SlideAction, wideSettingBounds);
                        if (option.SlideTooltip != null) slider.OnSliderTooltip = option.SlideTooltip;
                        composer.AddInteractiveElement(slider, option.Key);
                        break;

                    case ElementType.Switch:
                        composer.AddSwitch(option.ToggleAction, settingBounds, option.Key, switchSize);
                        break;

                    case ElementType.Dropdown:
                        composer.AddDropDown(
                            option.DropdownValues,
                            option.DropdownNames,
                            0,
                            option.DropdownSelect,
                            wideSettingBounds,
                            option.Key
                        );
                        break;

                    case ElementType.Custom:
                        IExtendedGuiElement element = option.CustomElement;
                        var bounds = option.Label ? settingBounds : textBounds;
                        element.Init(option.Key, bounds, composer);
                        composer.AddInteractiveElement((GuiElement)element, option.Key);
                        element.OnAdd(composer);
                        break;

                    default:
                        throw new Exception($"Unknown or missing element type: {option.Type}");
                }

                textBounds = textBounds.BelowCopy(fixedDeltaY: settingDeltaY);
                settingBounds = settingBounds.BelowCopy(fixedDeltaY: settingDeltaY);
            }

            SingleComposer = composer.EndChildElements().Compose();
        }

        public override bool TryOpen()
        {
            if (!isSetup)
                SetupDialog();

            var success = base.TryOpen();
            if (!success)
                return false;

            RefreshValues();
            return true;
        }

        public override bool TryClose()
        {
            ClientSettings.Save();
            return base.TryClose();
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }

        private void OnTabClicked(int dataIntOrIndex, GuiTab _)
        {
            ClientSettings.ActiveConfigTab = dataIntOrIndex;
            SetupDialog();
            RefreshValues();
        }

        protected abstract void RefreshValues();

        public override string ToggleKeyCombinationCode => null;
    }

    public class ModMenuDialog : ConfigDialog
    {
        private MicrophoneManager audioInputManager;
        private AudioOutputManager audioOutputManager;

        public ModMenuDialog(ICoreClientAPI capi, MicrophoneManager _audioInputManager, AudioOutputManager _audioOutputManager, ClientSettingsRepository settingsRepository) : base(capi)
        {
            audioInputManager = _audioInputManager;
            audioOutputManager = _audioOutputManager;

            var audioInputTab = new ConfigTab("AudioInput");
            var audioOutputTab = new ConfigTab("AudioOutput");
            var effectsTab = new ConfigTab("Effects");
            var interfaceTab = new ConfigTab("Interface");
            var playerListTab = new ConfigTab("PlayerList");
            var advancedTab = new ConfigTab("Advanced");
            RegisterTab(audioInputTab);
            RegisterTab(audioOutputTab);
            RegisterTab(effectsTab);
            RegisterTab(interfaceTab);
            RegisterTab(playerListTab);
            RegisterTab(advancedTab);

            RegisterOption(new ConfigOption
            {
                Key = "inputDevice",
                Type = ElementType.Dropdown,
                Label = true,
                Tooltip = true,
                Tab = audioInputTab,
                DropdownNames = audioInputManager.GetInputDeviceNames(),
                DropdownValues = audioInputManager.GetInputDeviceNames(),
                DropdownSelect = OnChangeInputDevice
            });

            RegisterOption(new ConfigOption
            {
                Key = "togglePushToTalk",
                Type = ElementType.Switch,
                Label = true,
                Tooltip = true,
                Tab = audioInputTab,
                ToggleAction = OnTogglePushToTalk
            });

            RegisterOption(new ConfigOption
            {
                Key = "muteMicrophone",
                Type = ElementType.Switch,
                Label = true,
                Tooltip = true,
                Tab = audioInputTab,
                ToggleAction = OnToggleMuted
            });

            RegisterOption(new ConfigOption
            {
                Key = "loopback",
                Type = ElementType.Switch,
                Label = true,
                Tooltip = true,
                Tab = audioOutputTab,
                ToggleAction = OnToggleLoopback
            });

            RegisterOption(new ConfigOption
            {
                Key = "outputGain",
                Type = ElementType.Slider,
                Label = true,
                Tooltip = true,
                Tab = audioOutputTab,
                SlideAction = SlideOutputGain
            });

            RegisterOption(new ConfigOption
            {
                Key = "inputGain",
                Type = ElementType.Slider,
                Label = true,
                Tooltip = true,
                Tab = audioInputTab,
                SlideAction = SlideInputGain,
                SlideTooltip = SlideInputGainTooltip
            });

            RegisterOption(new ConfigOption
            {
                Key = "inputThreshold",
                Type = ElementType.Slider,
                Label = true,
                Tooltip = true,
                Tab = audioInputTab,
                SlideAction = SlideInputThreshold
            });

            RegisterOption(new ConfigOption
            {
                Key = "audioMeter",
                Type = ElementType.Custom,
                Label = true,
                Tooltip = true,
                Tab = audioInputTab,
                CustomElement = new AudioMeter(capi, audioInputManager, this)
            });

            RegisterOption(new ConfigOption
            {
                Key = "toggleHUD",
                Type = ElementType.Switch,
                Label = true,
                Tooltip = true,
                Tab = interfaceTab,
                ToggleAction = OnToggleHUD
            });

            RegisterOption(new ConfigOption
            {
                Key = "toggleMuffling",
                Type = ElementType.Switch,
                Label = true,
                Tooltip = true,
                Tab = effectsTab,
                ToggleAction = OnToggleMuffling
            });

            RegisterOption(new ConfigOption
            {
                Enabled = audioInputManager.IsDenoisingAvailable,
                Key = "toggleDenoising",
                Type = ElementType.Switch,
                Label = true,
                Tooltip = true,
                Tab = effectsTab,
                ToggleAction = OnToggleDenoising
            });

            RegisterOption(new ConfigOption
            {
                Enabled = audioInputManager.IsDenoisingAvailable,
                Key = "denoisingSensitivity",
                Type = ElementType.Slider,
                Label = true,
                Tooltip = true,
                Tab = effectsTab,
                SlideAction = SlideDenoisingSensitivity
            });

            RegisterOption(new ConfigOption
            {
                Enabled = audioInputManager.IsDenoisingAvailable,
                Key = "denoisingStrength",
                Type = ElementType.Slider,
                Label = true,
                Tooltip = true,
                Tab = effectsTab,
                SlideAction = SlideDenoisingStrength
            });

            RegisterOption(new ConfigOption
            {
                Key = "playerList",
                Type = ElementType.Custom,
                Tab = playerListTab,
                CustomElement = new PlayerList(capi, settingsRepository, this)
            });

            RegisterOption(new ConfigOption
            {
                Key = "toggleChannelGuessing",
                Type = ElementType.Switch,
                Label = true,
                Tooltip = true,
                Tab = advancedTab,
                ToggleAction = OnToggleChannelGuessing
            });
        }

        protected override void RefreshValues()
        {
            if (!IsOpened())
                return;

            var outputGain = ClientSettings.OutputGain * 100;
            var inputGainDBS = AudioUtils.FactorToDBs(ClientSettings.InputGain) * 10;
            var inputThreshold = ClientSettings.InputThreshold * 100;
            var denoisingSensitivity = ClientSettings.BackgroundNoiseThreshold * 100;
            var denoisingStrength = ClientSettings.VoiceDenoisingStrength * 100;
            SetValue("configTabs", ClientSettings.ActiveConfigTab);
            SetValue("inputDevice", ClientSettings.CurrentInputDevice ?? "Default");
            SetValue("togglePushToTalk", ClientSettings.PushToTalkEnabled);
            SetValue("muteMicrophone", ClientSettings.IsMuted);
            SetValue("loopback", ClientSettings.Loopback);
            SetValue("outputGain", new dynamic[] { outputGain, 0, 200, 1, "%" });
            SetValue("inputGain", new dynamic[] { inputGainDBS, -200, 200, 1, "" });
            SetValue("inputThreshold", new dynamic[] { inputThreshold, 0, 100, 1, "" });
            SetValue("toggleHUD", ClientSettings.ShowHud);
            SetValue("toggleMuffling", ClientSettings.Muffling);
            SetValue("toggleDenoising", ClientSettings.Denoising);
            SetValue("denoisingSensitivity", new dynamic[] { denoisingSensitivity, 0, 100, 1, "%" });
            SetValue("denoisingStrength", new dynamic[] { denoisingStrength, 0, 100, 1, "%" });
            SetValue("playerList", null);
            SetValue("toggleChannelGuessing", ClientSettings.ChannelGuessing);
        }

        private void SetValue(string key, dynamic value)
        {
            GuiElement element = SingleComposer.GetElement(key);
            if (element is null) return;
            else if (element is GuiElementDropDown dropDown) dropDown.SetSelectedValue(value);
            else if (element is GuiElementSwitch switchBox) switchBox.On = value;
            else if (element is GuiElementSlider slider)
            {
                int sliderValue = GameMath.Clamp((int)value[0], value[1], value[2]);
                slider.SetValues(sliderValue, value[1], value[2], value[3], value[4]);
                if (value.Length < 6) return;
                slider.SetAlarmValue(value[5]);
            }
            else if (element is GuiElementVerticalTabs verticalTabs) verticalTabs.activeElement = value;
            else if (element is PlayerList playerList) playerList.SetupElement();
            else throw new Exception("Unknown element type");
        }

        private void OnChangeInputDevice(string value, bool selected)
        {
            audioInputManager.SetInputDevice(value);
        }

        private void OnTogglePushToTalk(bool enabled)
        {
            ClientSettings.PushToTalkEnabled = enabled;
        }

        private void OnToggleMuted(bool enabled)
        {
            ClientSettings.IsMuted = enabled;
            capi.Event.PushEvent("rpvoicechat:hudUpdate");
        }

        protected void OnToggleLoopback(bool enabled)
        {
            ClientSettings.Loopback = enabled;
            audioOutputManager.IsLoopbackEnabled = enabled;
        }

        private bool SlideOutputGain(int intGain)
        {
            float gain = (float)intGain / 100;
            ClientSettings.OutputGain = gain;
            PlayerListener.SetGain(gain);

            return true;
        }

        private bool SlideInputGain(int intDBGain)
        {
            float dBGain = (float)intDBGain / 10;
            float gain = AudioUtils.DBsToFactor(dBGain);
            if (intDBGain == -200) gain = 0;
            ClientSettings.InputGain = gain;
            audioInputManager.SetGain(gain);

            return true;
        }

        private string SlideInputGainTooltip(int value)
        {
            if (value == -200) return "Off";
            return SlideDecibelsTooltip(value);
        }

        private string SlideDecibelsTooltip(int value)
        {
            string sign = value < 0 ? "-" : "+";
            int integerPart = Math.Abs(value / 10);
            int decimalPart = Math.Abs(value % 10);
            string dBValueAsText = $"{sign}{integerPart}.{decimalPart}";
            return $"{dBValueAsText}dB";
        }

        private bool SlideInputThreshold(int intThreshold)
        {
            float threshold = (float)intThreshold / 100;
            ClientSettings.InputThreshold = threshold;
            audioInputManager.SetThreshold(threshold);

            return true;
        }

        private void OnToggleHUD(bool enabled)
        {
            ClientSettings.ShowHud = enabled;
            capi.Event.PushEvent("rpvoicechat:hudUpdate");
        }

        private void OnToggleMuffling(bool enabled)
        {
            ClientSettings.Muffling = enabled;
        }

        private void OnToggleDenoising(bool enabled)
        {
            ClientSettings.Denoising = enabled;
        }

        private bool SlideDenoisingSensitivity(int intSensitivity)
        {
            float sensitivity = (float)intSensitivity / 100;
            ClientSettings.BackgroundNoiseThreshold = sensitivity;
            audioInputManager.SetDenoisingSensitivity(sensitivity);

            return true;
        }

        private bool SlideDenoisingStrength(int intStrength)
        {
            float strength = (float)intStrength / 100;
            ClientSettings.VoiceDenoisingStrength = strength;
            audioInputManager.SetDenoisingStrength(strength);

            return true;
        }

        private void OnToggleChannelGuessing(bool enabled)
        {
            ClientSettings.ChannelGuessing = enabled;
        }
    }
}
