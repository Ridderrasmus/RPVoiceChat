using RPVoiceChat.Audio;
using RPVoiceChat.Config;
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
        private const string composerName = "RPVC_ModMenu";
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
        private List<GuiElementHoverText> hoverTextElements = new List<GuiElementHoverText>();

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
            public ActionConsumable ButtonAction;
            public CairoFont Font = CairoFont.WhiteSmallText();
            public string Text { get => UIUtils.I18n($"{i18nPrefix}.{Key}.Label"); }
            public string TooltipText { get => UIUtils.I18n($"{i18nPrefix}.{Key}.Tooltip"); }
            public string ButtonText { get => UIUtils.I18n($"{i18nPrefix}.{Key}.ButtonText"); }
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
            Button,
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
            hoverTextElements = new List<GuiElementHoverText>();

            var activeTab = ConfigTabs[ModConfig.ClientConfig.ActiveConfigTab];
            var displayedOptions = ConfigOptions.FindAll(e => e.Tab == activeTab && e.Enabled);
            double maxTextWidth = displayedOptions.DefaultIfEmpty().Max(e => e?.TextWidth ?? 0);
            double maxTabWidth = ConfigTabs.DefaultIfEmpty().Max(e => e?.TextWidth ?? 0);

            var tabsBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight + tabsTopPadding, maxTabWidth, tabsHeight).WithAlignment(EnumDialogArea.LeftTop);
            var textBounds = ElementBounds.Fixed(tabsBounds.fixedWidth + textLeftPadding, GuiStyle.TitleBarHeight, maxTextWidth, settingHeight);
            var settingBounds = ElementBounds.Fixed(textBounds.fixedWidth + textBounds.fixedX + settingsLeftPadding, GuiStyle.TitleBarHeight, 0, settingHeight);
            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing(ElementSizing.FitToChildren);

            var composer = capi.Gui.CreateCompo(composerName, ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n($"{i18nPrefix}.TitleBar"), OnTitleBarCloseClicked)
                .BeginChildElements(bgBounds)
                .AddVerticalTabs(ConfigTabs.ToArray(), tabsBounds, OnTabClicked, "configTabs");

            foreach (ConfigOption option in displayedOptions)
            {
                var wideSettingBounds = settingBounds.FlatCopy().WithFixedWidth(sliderWidth);

                if (option.Label) composer.AddStaticText(option.Text, option.Font, textBounds);
                if (option.Tooltip)
                {
                    composer.AddHoverText(option.TooltipText, option.Font, tooltipWidth, textBounds);
                    hoverTextElements.Add(composer.LastAddedElement as GuiElementHoverText);
                }

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

                    case ElementType.Button:
                        composer.AddSmallButton(option.ButtonText, option.ButtonAction, wideSettingBounds);
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

            foreach (var tab in ConfigTabs)
                if (tab != activeTab)
                    tab.Active = false;

            SingleComposer = composer.EndChildElements().Compose();
        }

        public override bool TryOpen()
        {
            if (!isSetup) SetupDialog();

            var success = base.TryOpen();
            if (!success) return false;

            RefreshValues();
            return true;
        }

        public override bool TryClose()
        {
            ModConfig.SaveClient(capi);
            return base.TryClose();
        }

        protected void DisableHoverText()
        {
            foreach (var element in hoverTextElements)
                element.SetAutoDisplay(false);
        }

        protected void EnableHoverText()
        {
            foreach (var element in hoverTextElements)
                element.SetAutoDisplay(true);
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }

        private void OnTabClicked(int dataIntOrIndex, GuiTab _)
        {
            ModConfig.ClientConfig.ActiveConfigTab = dataIntOrIndex;
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
        private GuiManager guiManager;

        public ModMenuDialog(ICoreClientAPI capi, MicrophoneManager _audioInputManager, AudioOutputManager _audioOutputManager, ClientSettingsRepository settingsRepository, GuiManager guiManager) : base(capi)
        {
            audioInputManager = _audioInputManager;
            audioOutputManager = _audioOutputManager;
            this.guiManager = guiManager;
            guiManager.audioWizardDialog.GainCalibrationDone += OnAudioWizardClosed;

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
                Key = "monoMode",
                Type = ElementType.Switch,
                Label = true,
                Tooltip = true,
                Tab = audioOutputTab,
                ToggleAction = OnToggleMonoMode
            });

            RegisterOption(new ConfigOption
            {
                Key = "outputVoice",
                Type = ElementType.Slider,
                Label = true,
                Tooltip = true,
                Tab = audioOutputTab,
                SlideAction = SlideOutputVoice
            });

            RegisterOption(new ConfigOption
            {
                Key = "outputBlock",
                Type = ElementType.Slider,
                Label = true,
                Tooltip = true,
                Tab = audioOutputTab,
                SlideAction = SlideOutputBlock
            });

            RegisterOption(new ConfigOption
            {
                Key = "outputItem",
                Type = ElementType.Slider,
                Label = true,
                Tooltip = true,
                Tab = audioOutputTab,
                SlideAction = SlideOutputItem
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
                Key = "audioWizard",
                Type = ElementType.Button,
                Label = true,
                Tooltip = true,
                Tab = audioInputTab,
                ButtonAction = OpenAudioWizard
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
                Key = "minimalHUD",
                Type = ElementType.Switch,
                Label = true,
                Tooltip = true,
                Tab = interfaceTab,
                ToggleAction = OnToggleMinimalHUD
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

        public override bool TryOpen()
        {
            bool otherDialogActive = audioInputManager.AudioWizardActive || guiManager.firstLaunchDialog.IsOpened();
            if (otherDialogActive) return true;

            return base.TryOpen();
        }

        public override bool TryClose()
        {
            if (audioInputManager.AudioWizardActive) return true;

            return base.TryClose();
        }

        protected override void RefreshValues()
        {
            if (!IsOpened())
                return;

            var outputVoice = ModConfig.ClientConfig.OutputVoice * 100;
            var outputBlock = ModConfig.ClientConfig.OutputBlock * 100;
            var outputItem = ModConfig.ClientConfig.OutputItem * 100;
            var inputGainDBS = AudioUtils.FactorToDBs(ModConfig.ClientConfig.InputGain) * 10;
            var inputThreshold = ModConfig.ClientConfig.InputThreshold * 100;
            var denoisingSensitivity = ModConfig.ClientConfig.DenoisingSensitivity * 100;
            var denoisingStrength = ModConfig.ClientConfig.DenoisingStrength * 100;
            SetValue("configTabs", ModConfig.ClientConfig.ActiveConfigTab);
            SetValue("inputDevice", ModConfig.ClientConfig.InputDevice ?? "Default");
            SetValue("togglePushToTalk", ModConfig.ClientConfig.PushToTalkEnabled);
            SetValue("muteMicrophone", ModConfig.ClientConfig.IsMuted);
            SetValue("loopback", ModConfig.ClientConfig.Loopback);
            SetValue("monoMode", ModConfig.ClientConfig.IsMonoMode);
            SetValue("outputVoice", new dynamic[] { outputVoice, 0, 200, 1, "%" });
            SetValue("inputGain", new dynamic[] { inputGainDBS, -200, 200, 1, "" });
            SetValue("outputBlock", new dynamic[] { outputBlock, 0, 200, 1, "%" });
            SetValue("outputItem", new dynamic[] { outputItem, 0, 200, 1, "%" });
            SetValue("inputThreshold", new dynamic[] { inputThreshold, 0, 100, 1, "" });
            SetValue("toggleHUD", ModConfig.ClientConfig.ShowHud);
            SetValue("minimalHUD", ModConfig.ClientConfig.IsMinimalHud);
            SetValue("toggleMuffling", ModConfig.ClientConfig.Muffling);
            SetValue("toggleDenoising", ModConfig.ClientConfig.Denoising);
            SetValue("denoisingSensitivity", new dynamic[] { denoisingSensitivity, 0, 100, 1, "%" });
            SetValue("denoisingStrength", new dynamic[] { denoisingStrength, 0, 100, 1, "%" });
            SetValue("playerList", null);
            SetValue("toggleChannelGuessing", ModConfig.ClientConfig.ChannelGuessing);

            if (audioInputManager.AudioWizardActive) DisableHoverText();
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
            else if (element is GuiElementVerticalTabs verticalTabs) verticalTabs.ActiveElement = value;
            else if (element is PlayerList playerList) playerList.SetupElement();
            else throw new Exception("Unknown element type");
        }

        private void OnAudioWizardClosed()
        {
            EnableHoverText();
            RefreshValues();
        }

        private void OnChangeInputDevice(string value, bool selected)
        {
            audioInputManager.SetInputDevice(value);
            SetValue("inputDevice", ModConfig.ClientConfig.InputDevice ?? "Default");
        }

        private void OnTogglePushToTalk(bool enabled)
        {
            ModConfig.ClientConfig.PushToTalkEnabled = enabled;
        }

        private void OnToggleMuted(bool enabled)
        {
            ModConfig.ClientConfig.IsMuted = enabled;
            capi.Event.PushEvent("rpvoicechat:hudUpdate");
        }

        protected void OnToggleLoopback(bool enabled)
        {
            ModConfig.ClientConfig.Loopback = enabled;
            audioOutputManager.IsLoopbackEnabled = enabled;
        }

        protected void OnToggleMonoMode(bool enabled)
        {
            ModConfig.ClientConfig.IsMonoMode = enabled;
            capi.Event.PushEvent("rpvoicechat:hudUpdate");
        }

        protected void OnToggleMinimalHUD(bool enabled)
        {
            ModConfig.ClientConfig.IsMinimalHud = enabled;
            capi.Event.PushEvent("rpvoicechat:hudUpdate");
        }

        private bool SlideOutputVoice(int intGain)
        {
            float gain = intGain / 100f;
            ModConfig.ClientConfig.OutputVoice = gain;
            PlayerListener.SetVoiceGain(gain);

            return true;
        }

        private bool SlideOutputBlock(int intGain)
        {
            float gain = intGain / 100f;
            ModConfig.ClientConfig.OutputBlock = gain;
            PlayerListener.SetBlockGain(gain);

            return true;
        }

        private bool SlideOutputItem(int intGain)
        {
            float gain = intGain / 100f;
            ModConfig.ClientConfig.OutputItem = gain;
            PlayerListener.SetItemGain(gain);

            return true;
        }

        private bool SlideInputGain(int intDBGain)
        {
            float dBGain = (float)intDBGain / 10;
            float gain = AudioUtils.DBsToFactor(dBGain);
            if (intDBGain == -200) gain = 0;
            ModConfig.ClientConfig.InputGain = gain;
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
            ModConfig.ClientConfig.InputThreshold = threshold;
            audioInputManager.SetThreshold(threshold);

            return true;
        }

        private bool OpenAudioWizard()
        {
            if (guiManager.audioWizardDialog.IsOpened()) return true;
            DisableHoverText();
            guiManager.audioWizardDialog.TryOpen();

            return true;
        }

        private void OnToggleHUD(bool enabled)
        {
            ModConfig.ClientConfig.ShowHud = enabled;
            capi.Event.PushEvent("rpvoicechat:hudUpdate");
        }

        private void OnToggleMuffling(bool enabled)
        {
            ModConfig.ClientConfig.Muffling = enabled;
        }

        private void OnToggleDenoising(bool enabled)
        {
            ModConfig.ClientConfig.Denoising = enabled;
        }

        private bool SlideDenoisingSensitivity(int intSensitivity)
        {
            float sensitivity = (float)intSensitivity / 100;
            ModConfig.ClientConfig.DenoisingSensitivity = sensitivity;
            audioInputManager.SetDenoisingSensitivity(sensitivity);

            return true;
        }

        private bool SlideDenoisingStrength(int intStrength)
        {
            float strength = (float)intStrength / 100;
            ModConfig.ClientConfig.DenoisingStrength = strength;
            audioInputManager.SetDenoisingStrength(strength);

            return true;
        }

        private void OnToggleChannelGuessing(bool enabled)
        {
            ModConfig.ClientConfig.ChannelGuessing = enabled;
        }
    }
}
