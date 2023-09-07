using RPVoiceChat.Audio;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public abstract class ConfigDialog : GuiDialog
    {

        private bool _isSetup;

        protected List<ConfigOption> ConfigOptions = new List<ConfigOption>();


        public MicrophoneManager _audioInputManager;
        public AudioOutputManager _audioOutputManager;

        private GuiElementAudioMeter AudioMeter;
        private long audioMeterId;

        public class ConfigOption
        {
            public ActionConsumable AdvancedAction;
            public string SwitchKey;
            public string SliderKey;
            public string SpecialSliderKey;
            public string DropdownKey;

            public string Text;

            public Action<bool> ToggleAction;
            public ActionConsumable<int> SlideAction;
            public SelectionChangedDelegate DropdownSelect { get; internal set; }


            public string Tooltip;
            public bool InstantSlider;

            public string[] DropdownValues { get; internal set; }
            public string[] DropdownNames { get; internal set; }
        }


        public ConfigDialog(ICoreClientAPI capi) : base(capi)
        {

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

            var switchBounds = ElementBounds.Fixed(250, GuiStyle.TitleBarHeight, 300, switchSize);
            var textBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, 210, switchSize);

            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var composer = capi.Gui.CreateCompo("rpvcconfigmenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("RP VC Config Menu", OnTitleBarCloseClicked)
                .BeginChildElements(bgBounds);

            foreach (ConfigOption option in ConfigOptions)
            {
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

            //foreach (var option in ConfigOptions.Where(option => option.SliderKey != null && !option.InstantSlider))
            //{
            //    SingleComposer.GetSlider(option.SliderKey);
            //}
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

            RegisterOption(new ConfigOption
            {
                Text = "Input Device",
                DropdownKey = "inputDevice",
                Tooltip = "Input device",
                DropdownNames = _audioInputManager.GetInputDeviceNames(),
                DropdownValues = _audioInputManager.GetInputDeviceNames(),
                DropdownSelect = OnChangeInputDevice
            });

            RegisterOption(new ConfigOption
            {
                Text = "Push To Talk",
                SwitchKey = "togglePushToTalk",
                Tooltip = "Use push to talk instead of voice activation",
                ToggleAction = OnTogglePushToTalk
            });

            RegisterOption(new ConfigOption
            {
                Text = "Mute",
                SwitchKey = "muteMicrophone",
                Tooltip = "Mute your microphone",
                ToggleAction = OnToggleMuted
            });

            RegisterOption(new ConfigOption
            {
                Text = "Loopback",
                SwitchKey = "loopback",
                Tooltip = "Play recorded audio through output audio",
                ToggleAction = OnToggleLoopback
            });

            RegisterOption(new ConfigOption
            {
                Text = "Audio Input Threshold",
                SliderKey = "inputThreshold",
                Tooltip = "At which threshold your audio starts transmitting",
                SlideAction = SlideInputThreshold
            });

            RegisterOption(new ConfigOption
            {
                Text = "Audio Meter",
                SpecialSliderKey = "audioMeter",
                Tooltip = "Shows your audio amplitude"
            });

            RegisterOption(new ConfigOption
            {
                Text = "Toggle HUD",
                SwitchKey = "toggleHUD",
                Tooltip = "Toggle visibility of HUD elements",
                ToggleAction = OnToggleHUD
            });
        }

        protected override void RefreshValues()
        {
            if (!IsOpened())
                return;

            SingleComposer.GetSwitch("togglePushToTalk").On = _config.PushToTalkEnabled;
            SingleComposer.GetSwitch("muteMicrophone").On = _config.IsMuted;
            SingleComposer.GetSwitch("toggleHUD").On = _config.IsHUDShown;
            SingleComposer.GetSlider("inputThreshold").SetValues(_config.InputThreshold, 0, 100, 1);
            SingleComposer.GetDropDown("inputDevice").SetSelectedValue(_config.CurrentInputDevice);
            SingleComposer.GetSwitch("loopback").On = _config.IsLoopbackEnabled;
        }

        private void OnToggleHUD(bool enabled)
        {
            _config.IsHUDShown = enabled;
            ModConfig.Save(capi);
            _audioInputManager.SetVoiceLevel(_audioInputManager.GetVoiceLevel());
        }

        protected void OnToggleLoopback(bool enabled)
        {
            _config.IsLoopbackEnabled = enabled;
            ModConfig.Save(capi);
            _audioOutputManager.IsLoopbackEnabled = enabled;
        }

        private void OnChangeInputDevice(string value, bool selected)
        {
            _audioInputManager.SetInputDevice(value);
        }

        private bool SlideInputThreshold(int threshold)
        {
            _config.InputThreshold = threshold;
            _audioInputManager.SetThreshold(threshold);
            ModConfig.Save(capi);

            return true;
        }

        private void OnToggleMuted(bool enabled)
        {
            _config.IsMuted = enabled;
            ModConfig.Save(capi);
        }

        private void OnTogglePushToTalk(bool enabled)
        {
            _config.PushToTalkEnabled = enabled;
            ModConfig.Save(capi);
        }
    }
}
