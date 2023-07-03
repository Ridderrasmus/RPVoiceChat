using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace rpvoicechat
{
    public abstract class ConfigDialog : GuiDialog
    {

        private bool _isSetup;

        protected List<ConfigOption> ConfigOptions = new List<ConfigOption>();

        public MicrophoneManager _audioInputManager;

        public class ConfigOption
        {
            public ActionConsumable AdvancedAction;
            public string SwitchKey;
            public string SliderKey;
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

            var switchBounds = ElementBounds.Fixed(250, GuiStyle.TitleBarHeight, switchSize, switchSize);
            var textBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, 110, switchSize);

            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var composer = capi.Gui.CreateCompo("rphudmenu", dialogBounds)
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
                    composer.AddDropDown(option.DropdownValues, option.DropdownNames, 0, option.DropdownSelect, textBounds);
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

        protected abstract void RefreshValues();

        public override string ToggleKeyCombinationCode => null;
    }

    public class MainConfig : ConfigDialog
    {
        public MainConfig(ICoreClientAPI capi, MicrophoneManager audioInputManager) : base(capi)
        {

            _audioInputManager = audioInputManager;
            RegisterOption(new ConfigOption
            {
                Text = "Push To Talk",
                SwitchKey = "togglePushToTalk",
                Tooltip = "Use push to talk instead of voice activation",
                ToggleAction = togglePushToTalk
            });

            RegisterOption(new ConfigOption
            {
                Text = "Mute",
                SwitchKey = "muteMicrophone",
                Tooltip = "Mute your microphone",
                ToggleAction = toggleMuted
            });

            RegisterOption(new ConfigOption
            {
                Text = "Audio Input Threshold",
                SliderKey = "inputThreshold",
                Tooltip = "At which threshold your audio starts transmitting",
                SlideAction = slideInputThreshold
            });

            /*RegisterOption(new ConfigOption
            {
                Text = "Input Device",
                DropdownKey = "inputDevice",
                Tooltip = "Input device",
                DropdownNames = _audioInputManager.GetInputDeviceNames(),
                DropdownValues = _audioInputManager.GetInputDeviceIds(),
                DropdownSelect = changeInputDevice
            });*/
        }

        protected override void RefreshValues()
        {
            if (!IsOpened())
                return;

            SingleComposer.GetSwitch("togglePushToTalk").On = _audioInputManager.pushToTalkEnabled;
            SingleComposer.GetSwitch("muteMicrophone").On = _audioInputManager.isMuted;
            SingleComposer.GetSlider("inputThreshold").SetValues(_audioInputManager.inputThreshold, 0, 100, 1);
            //SingleComposer.GetDropDown("inputDevice").SetSelectedIndex((RPModSettings.CurrentInputDevice).ToInt(0));
        }

        private void changeInputDevice(string deviceId, bool selected)
        {
            _audioInputManager.CurrentInputDevice = deviceId;
            _audioInputManager.SetInputDevice(deviceId);
        }

        private bool slideInputThreshold(int threshold)
        {
            _audioInputManager.inputThreshold = threshold;


            return true;
        }

        private void toggleMuted(bool enabled)
        {
            _audioInputManager.isMuted = enabled;
        }

        private void togglePushToTalk(bool enabled)
        {
            _audioInputManager.pushToTalkEnabled = enabled;
        }
    }
}
