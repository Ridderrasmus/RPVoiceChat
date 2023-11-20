using RPVoiceChat.Audio;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Gui
{
    public class AudioWizardDialog : GuiDialog
    {
        public event Action GainCalibrationDone;
        public override double DrawOrder => 0.11;
        private const string i18nPrefix = "Gui.AudioWizardDialog";
        private const string composerName = "RPVC_AudioWizardDialog";
        private const int textYOffset = 5;
        private const int textLeftPadding = 5;
        private const int textBottomPadding = 15;
        private const int textWidth = 460;
        private const int defaultElementHeight = 30;
        private const int buttonXPadding = 10;
        private const int buttonYPadding = 2;
        private const int gainCalibrationDuration = 7000;
        private const int calibrationUpdateInterval = 50;
        private const int calibrationSteps = gainCalibrationDuration / calibrationUpdateInterval;
        private MicrophoneManager audioInputManager;
        private AudioOutputManager audioOutputManager;
        private CancellationTokenSource configurationCTS;
        private GuiDialog doneDialog;
        private float adjustedGain;
        private bool configurationInProcess = false;

        public AudioWizardDialog(ICoreClientAPI capi, MicrophoneManager audioInputManager, AudioOutputManager audioOutputManager) : base(capi)
        {
            this.audioInputManager = audioInputManager;
            this.audioOutputManager = audioOutputManager;
            doneDialog = new AudioWizardDoneDialog(capi);
            doneDialog.OnClosed += SaveAndExit;
        }

        public override bool TryOpen()
        {
            audioInputManager.AudioWizardActive = true;
            configurationCTS = new CancellationTokenSource();
            if (ClientSettings.InputGain == 0)
                audioInputManager.SetGain(1);
            adjustedGain = ClientSettings.InputGain;
            ClientSettings.Loopback = true;
            audioOutputManager.IsLoopbackEnabled = true;
            Compose();
            return base.TryOpen();
        }

        public override bool TryClose()
        {
            configurationCTS.Cancel();
            configurationCTS.Dispose();
            configurationInProcess = false;
            ClientSettings.InputGain = adjustedGain;
            audioInputManager.SetGain(adjustedGain);
            if (doneDialog.IsOpened() == false) SaveAndExit();
            return base.TryClose();
        }

        private void Compose()
        {
            var drawUtil = new TextDrawUtil();
            var font = CairoFont.WhiteSmallText();
            var dropdownValues = audioInputManager.GetInputDeviceNames();

            var titleBarText = UIUtils.I18n($"{i18nPrefix}.TitleBar");
            var firstTextBlock = UIUtils.I18n($"{i18nPrefix}.FirstParagraph");
            var secondTextBlock = UIUtils.I18n($"{i18nPrefix}.SecondParagraph");
            var startButtonText = Lang.Get("Start");
            var firstTextBlockHeight = drawUtil.GetMultilineTextHeight(font, firstTextBlock, textWidth);
            var secondTextBlockHeight = drawUtil.GetMultilineTextHeight(font, secondTextBlock, textWidth);

            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing(ElementSizing.FitToChildren);
            var firstTextBlockBounds = ElementBounds.Fixed(textLeftPadding, GuiStyle.TitleBarHeight + textYOffset, textWidth, firstTextBlockHeight);
            var dropdownBounds = firstTextBlockBounds.BelowCopy(0, textBottomPadding).WithFixedHeight(defaultElementHeight);
            var secondTextBlockBounds = dropdownBounds.BelowCopy(0, textBottomPadding).WithFixedHeight(secondTextBlockHeight);
            var progressBarBounds = secondTextBlockBounds.BelowCopy(-textLeftPadding, textBottomPadding).WithFixedHeight(defaultElementHeight);
            var buttonBounds = progressBarBounds.BelowCopy(0, textBottomPadding).WithFixedSize(0, defaultElementHeight).WithFixedPadding(buttonXPadding, buttonYPadding).WithAlignment(EnumDialogArea.CenterFixed);

            var progressBar = new GuiElementStatbar(capi, progressBarBounds, new double[3] { 0.1, 0.4, 0.1 }, false, false);
            progressBar.ShowValueOnHover = false;

            SingleComposer = capi.Gui.CreateCompo(composerName, ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleBarText, () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddStaticText(firstTextBlock, font, firstTextBlockBounds)
                    .AddDropDown(dropdownValues, dropdownValues, 0, OnDropdownSelect, dropdownBounds, "inputDevice")
                    .AddStaticText(secondTextBlock, font, secondTextBlockBounds)
                    .AddInteractiveElement(progressBar, "progressBar")
                    .AddButton(startButtonText, OnStartButtonClick, buttonBounds)
                .EndChildElements()
                .Compose();

            progressBar.SetValues(0, 0, calibrationSteps);
            progressBar.SetLineInterval(calibrationSteps / 10);
            var inputDeviceDropdown = SingleComposer.GetDropDown("inputDevice");
            inputDeviceDropdown.SetSelectedValue(ClientSettings.CurrentInputDevice ?? "Default");
        }

        private bool OnStartButtonClick()
        {
            if (configurationInProcess) return true;
            configurationInProcess = true;

            float maxGain = AudioUtils.DBsToFactor(20);
            audioInputManager.SetGain(maxGain);
            StartGainConfiguration();

            return true;
        }

        private async void StartGainConfiguration()
        {
            var progressBar = SingleComposer.GetStatbar("progressBar");
            var effectiveGains = new List<float>();

            audioInputManager.GetRecentGainLimits();
            for (var i = 0; i < calibrationSteps; i++)
            {
                if (configurationCTS.IsCancellationRequested) return;

                var recentEffectiveGains = audioInputManager.GetRecentGainLimits();
                effectiveGains.AddRange(recentEffectiveGains);

                progressBar.SetValue(i + 1);
                await Task.Delay(calibrationUpdateInterval);
            }

            effectiveGains.Sort();
            float lowerQuartileGain = effectiveGains[effectiveGains.Count / 4];
            float newGain = AudioUtils.FactorToDBs(lowerQuartileGain);
            newGain = GameMath.Clamp(newGain, -20, 20);
            newGain = AudioUtils.DBsToFactor(newGain);
            adjustedGain = newGain;

            doneDialog.TryOpen();
            TryClose();
        }

        private void SaveAndExit()
        {
            audioInputManager.AudioWizardActive = false;
            ClientSettings.Loopback = false;
            audioOutputManager.IsLoopbackEnabled = false;
            ClientSettings.Save();
            GainCalibrationDone?.Invoke();
        }

        private void OnDropdownSelect(string value, bool selected)
        {
            audioInputManager.SetInputDevice(value);
            var dropdown = SingleComposer.GetDropDown("inputDevice");
            dropdown.SetSelectedValue(ClientSettings.CurrentInputDevice ?? "Default");
        }

        public override string ToggleKeyCombinationCode => null;

        private class AudioWizardDoneDialog : GuiDialog
        {
            public override double DrawOrder => 0.11;
            private const string composerName = "RPVC_AudioWizardDoneDialog";

            public AudioWizardDoneDialog(ICoreClientAPI capi) : base(capi)
            {
                var drawUtil = new TextDrawUtil();
                var font = CairoFont.WhiteSmallishText();

                var titleBarText = UIUtils.I18n($"{i18nPrefix}.TitleBar");
                var firstTextBlock = UIUtils.I18n($"{i18nPrefix}.Done");
                var okButtonText = Lang.Get("Ok");
                var firstTextBlockHeight = drawUtil.GetMultilineTextHeight(font, firstTextBlock, textWidth);

                var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing(ElementSizing.FitToChildren);
                var firstTextBlockBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, textWidth, firstTextBlockHeight);
                var buttonBounds = firstTextBlockBounds.BelowCopy(0, textBottomPadding).WithFixedSize(0, defaultElementHeight).WithFixedPadding(buttonXPadding, buttonYPadding).WithAlignment(EnumDialogArea.CenterFixed);

                SingleComposer = capi.Gui.CreateCompo(composerName, ElementStdBounds.AutosizedMainDialog)
                    .AddShadedDialogBG(bgBounds)
                    .AddDialogTitleBar(titleBarText, () => TryClose())
                    .BeginChildElements(bgBounds)
                        .AddStaticText(firstTextBlock, font, EnumTextOrientation.Center, firstTextBlockBounds)
                        .AddButton(okButtonText, TryClose, buttonBounds)
                    .EndChildElements()
                    .Compose();
            }

            public override string ToggleKeyCombinationCode => null;
        }
    }
}
