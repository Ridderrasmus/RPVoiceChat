using RPVoiceChat.Audio;
using RPVoiceChat.Config;
using RPVoiceChat.Util;
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
        private const int gainCalibrationDuration = 4000;
        private const int thresholdCalibrationDuration = 4000;
        private const int calibrationUpdateInterval = 50;
        private const int gainCalibrationSteps = gainCalibrationDuration / calibrationUpdateInterval;
        private const int thresholdCalibrationSteps = thresholdCalibrationDuration / calibrationUpdateInterval;
        private const int totalCalibrationSteps = gainCalibrationSteps + thresholdCalibrationSteps;
        private MicrophoneManager audioInputManager;
        private AudioOutputManager audioOutputManager;
        private CancellationTokenSource configurationCTS;
        private GuiDialog doneDialog;
        private GuiElementDynamicText wizardStatusText;
        private float adjustedGain;
        private float adjustedThreshold;
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
            if (ModConfig.ClientConfig.InputGain == 0)
                audioInputManager.SetGain(1);
            adjustedGain = ModConfig.ClientConfig.InputGain;
            adjustedThreshold = ModConfig.ClientConfig.InputThreshold;
            ModConfig.ClientConfig.Loopback = true;
            audioOutputManager.IsLoopbackEnabled = true;
            Compose();
            return base.TryOpen();
        }

        public override bool TryClose()
        {
            configurationCTS.Cancel();
            configurationCTS.Dispose();
            configurationInProcess = false;
            ModConfig.ClientConfig.InputGain = adjustedGain;
            ModConfig.ClientConfig.InputThreshold = adjustedThreshold;
            audioInputManager.SetGain(adjustedGain);
            audioInputManager.SetThreshold(adjustedThreshold);
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
            var statusTextBounds = progressBarBounds.BelowCopy(0, 8).WithFixedHeight(defaultElementHeight);
            var buttonBounds = statusTextBounds.BelowCopy(0, textBottomPadding).WithFixedSize(0, defaultElementHeight).WithFixedPadding(buttonXPadding, buttonYPadding).WithAlignment(EnumDialogArea.CenterFixed);

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
                    .AddDynamicText("", CairoFont.WhiteSmallText(), statusTextBounds, "wizardStatusText")
                    .AddButton(startButtonText, OnStartButtonClick, buttonBounds)
                .EndChildElements()
                .Compose();

            progressBar.SetValues(0, 0, totalCalibrationSteps);
            progressBar.SetLineInterval(totalCalibrationSteps / 10);
            wizardStatusText = SingleComposer.GetDynamicText("wizardStatusText");
            var inputDeviceDropdown = SingleComposer.GetDropDown("inputDevice");
            inputDeviceDropdown.SetSelectedValue(ModConfig.ClientConfig.InputDevice ?? "Default");
        }

        private bool OnStartButtonClick()
        {
            if (configurationInProcess) return true;
            configurationInProcess = true;
            wizardStatusText?.SetNewText(UIUtils.I18n($"{i18nPrefix}.Status.CalibratingGain"));

            float maxGain = AudioUtils.DBsToFactor(20);
            audioInputManager.SetGain(maxGain);
            audioInputManager.ClearCalibrationSamples();
            StartCalibration();

            return true;
        }

        private async void StartCalibration()
        {
            var progressBar = SingleComposer.GetStatbar("progressBar");
            var effectiveGains = new List<float>();
            var amplitudes = new List<double>();
            try
            {
                audioInputManager.GetRecentGainLimits();
                audioInputManager.GetRecentAmplitudes();

                for (var i = 0; i < gainCalibrationSteps; i++)
                {
                    if (configurationCTS.IsCancellationRequested) return;

                    effectiveGains.AddRange(audioInputManager.GetRecentGainLimits());
                    int step = i + 1;
                    await RunOnMainThread(() => progressBar.SetValue(step));
                    await Task.Delay(calibrationUpdateInterval);
                }

                if (effectiveGains.Count == 0)
                {
                    await RunOnMainThread(() =>
                        wizardStatusText?.SetNewText(UIUtils.I18n($"{i18nPrefix}.NoInputData")));
                    return;
                }

                adjustedGain = ComputeCalibratedGain(effectiveGains);
                audioInputManager.SetGain(adjustedGain);
                audioInputManager.ClearCalibrationSamples();
                await RunOnMainThread(() =>
                    wizardStatusText?.SetNewText(UIUtils.I18n($"{i18nPrefix}.Status.CalibratingThreshold")));

                // Let a couple of frames settle under the new gain before sampling amplitudes.
                await Task.Delay(calibrationUpdateInterval * 2);
                audioInputManager.GetRecentAmplitudes();

                for (var i = 0; i < thresholdCalibrationSteps; i++)
                {
                    if (configurationCTS.IsCancellationRequested) return;

                    amplitudes.AddRange(audioInputManager.GetRecentAmplitudes());
                    int step = gainCalibrationSteps + i + 1;
                    await RunOnMainThread(() => progressBar.SetValue(step));
                    await Task.Delay(calibrationUpdateInterval);
                }

                if (amplitudes.Count == 0)
                {
                    await RunOnMainThread(() =>
                        wizardStatusText?.SetNewText(UIUtils.I18n($"{i18nPrefix}.NoInputData")));
                    return;
                }

                adjustedThreshold = ComputeCalibratedThreshold(amplitudes);
                audioInputManager.SetThreshold(adjustedThreshold);

                await RunOnMainThread(() =>
                {
                    doneDialog.TryOpen();
                    TryClose();
                });
            }
            finally
            {
                configurationInProcess = false;
            }
        }

        private Task RunOnMainThread(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            capi.Event.EnqueueMainThreadTask(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }, "rpvoicechat:AudioWizard");
            return tcs.Task;
        }

        private static float ComputeCalibratedGain(List<float> effectiveGains)
        {
            effectiveGains.Sort();
            float lowerQuartileGain = effectiveGains[effectiveGains.Count / 4];
            float newGain = AudioUtils.FactorToDBs(lowerQuartileGain);
            newGain = GameMath.Clamp(newGain, -20, 20);
            return AudioUtils.DBsToFactor(newGain);
        }

        private float ComputeCalibratedThreshold(List<double> amplitudes)
        {
            amplitudes.Sort();
            int count = amplitudes.Count;
            double noiseFloor = amplitudes[Math.Max(0, count / 10)];
            double speechLevel = amplitudes[Math.Min(count - 1, (count * 6) / 10)];
            double absoluteThreshold = noiseFloor + (speechLevel - noiseFloor) * 0.35;
            // Prefer opening slightly early over late (reduces first-syllable doubling).
            absoluteThreshold = Math.Min(absoluteThreshold, speechLevel * 0.55);

            double maxThreshold = audioInputManager.GetMaxInputThreshold();
            if (maxThreshold <= 0) return ModConfig.ClientConfig.InputThreshold;

            float normalized = (float)(absoluteThreshold / maxThreshold);
            return GameMath.Clamp(normalized, 0.08f, 0.75f);
        }

        private void SaveAndExit()
        {
            audioInputManager.AudioWizardActive = false;
            ModConfig.ClientConfig.Loopback = false;
            audioOutputManager.IsLoopbackEnabled = false;
            ModConfig.ClientConfig.InputGain = adjustedGain;
            ModConfig.ClientConfig.InputThreshold = adjustedThreshold;
            audioInputManager.SetGain(adjustedGain);
            audioInputManager.SetThreshold(adjustedThreshold);
            ModConfig.SaveClient(capi);
            GainCalibrationDone?.Invoke();
        }

        private void OnDropdownSelect(string value, bool selected)
        {
            audioInputManager.SetInputDevice(value);
            var dropdown = SingleComposer.GetDropDown("inputDevice");
            dropdown.SetSelectedValue(ModConfig.ClientConfig.InputDevice ?? "Default");
        }

        public override string ToggleKeyCombinationCode => null;

        public override void Dispose()
        {
            doneDialog?.Dispose();
            base.Dispose();
        }

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
