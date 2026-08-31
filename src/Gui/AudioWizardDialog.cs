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
        private bool noMicrophoneMode = false;

        public AudioWizardDialog(ICoreClientAPI capi, MicrophoneManager audioInputManager, AudioOutputManager audioOutputManager) : base(capi)
        {
            this.audioInputManager = audioInputManager;
            this.audioOutputManager = audioOutputManager;
            doneDialog = new AudioWizardDoneDialog(capi);
            doneDialog.OnClosed += SaveAndExit;
        }

        public override bool TryOpen()
        {
            configurationCTS = new CancellationTokenSource();
            adjustedGain = ModConfig.ClientConfig.InputGain;
            adjustedThreshold = ModConfig.ClientConfig.InputThreshold;
            noMicrophoneMode = !audioInputManager.CanUseMicrophoneCapture();

            if (noMicrophoneMode)
            {
                ComposeNoMicrophone();
                return base.TryOpen();
            }

            audioInputManager.AudioWizardActive = true;
            // Saved gain 0 silences loopback; use unity gain so the wizard preview can be heard.
            if (ModConfig.ClientConfig.InputGain == 0)
            {
                audioInputManager.SetGain(1);
            }

            ModConfig.ClientConfig.Loopback = true;
            audioOutputManager.IsLoopbackEnabled = true;
            Compose();
            return base.TryOpen();
        }

        public override bool TryClose()
        {
            configurationCTS?.Cancel();
            configurationCTS?.Dispose();
            configurationInProcess = false;

            if (!noMicrophoneMode)
            {
                ModConfig.ClientConfig.InputGain = adjustedGain;
                ModConfig.ClientConfig.InputThreshold = adjustedThreshold;
                audioInputManager.SetGain(adjustedGain);
                audioInputManager.SetThreshold(adjustedThreshold);
                if (doneDialog.IsOpened() == false)
                {
                    SaveAndExit();
                }
            }

            noMicrophoneMode = false;
            return base.TryClose();
        }

        private void ComposeNoMicrophone()
        {
            var drawUtil = new TextDrawUtil();
            var font = CairoFont.WhiteSmallText();
            var titleBarText = UIUtils.I18n($"{i18nPrefix}.TitleBar");
            var bodyText = UIUtils.I18n($"{i18nPrefix}.NoMicrophone");
            var skipButtonText = UIUtils.I18n($"{i18nPrefix}.Skip");
            var bodyHeight = drawUtil.GetMultilineTextHeight(font, bodyText, textWidth);

            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing(ElementSizing.FitToChildren);
            var bodyBounds = ElementBounds.Fixed(textLeftPadding, GuiStyle.TitleBarHeight + textYOffset, textWidth, bodyHeight);
            var buttonBounds = bodyBounds.BelowCopy(0, textBottomPadding)
                .WithFixedSize(0, defaultElementHeight)
                .WithFixedPadding(buttonXPadding, buttonYPadding)
                .WithAlignment(EnumDialogArea.CenterFixed);

            SingleComposer = capi.Gui.CreateCompo(composerName, ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleBarText, () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddStaticText(bodyText, font, bodyBounds)
                    .AddButton(skipButtonText, () => TryClose(), buttonBounds)
                .EndChildElements()
                .Compose();
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
            var skipButtonText = UIUtils.I18n($"{i18nPrefix}.Skip");
            var firstTextBlockHeight = drawUtil.GetMultilineTextHeight(font, firstTextBlock, textWidth);
            var secondTextBlockHeight = drawUtil.GetMultilineTextHeight(font, secondTextBlock, textWidth);

            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing(ElementSizing.FitToChildren);
            var firstTextBlockBounds = ElementBounds.Fixed(textLeftPadding, GuiStyle.TitleBarHeight + textYOffset, textWidth, firstTextBlockHeight);
            var dropdownBounds = firstTextBlockBounds.BelowCopy(0, textBottomPadding).WithFixedHeight(defaultElementHeight);
            var secondTextBlockBounds = dropdownBounds.BelowCopy(0, textBottomPadding).WithFixedHeight(secondTextBlockHeight);
            var progressBarBounds = secondTextBlockBounds.BelowCopy(-textLeftPadding, textBottomPadding).WithFixedHeight(defaultElementHeight);
            var statusTextBounds = progressBarBounds.BelowCopy(0, 8).WithFixedHeight(defaultElementHeight);
            var buttonBounds = statusTextBounds.BelowCopy(0, textBottomPadding)
                .WithFixedSize(0, defaultElementHeight)
                .WithFixedPadding(buttonXPadding, buttonYPadding);

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
                    .AddButton(skipButtonText, () => TryClose(), buttonBounds.FlatCopy().WithAlignment(EnumDialogArea.RightFixed))
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
            if (configurationInProcess || noMicrophoneMode) return true;
            if (!audioInputManager.CanUseMicrophoneCapture())
            {
                wizardStatusText?.SetNewText(UIUtils.I18n($"{i18nPrefix}.NoMicrophone"));
                return true;
            }

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
                    await RunOnMainThread(() => SetProgressValue(step));
                    await Task.Delay(calibrationUpdateInterval, configurationCTS.Token);
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

                await Task.Delay(calibrationUpdateInterval * 2, configurationCTS.Token);
                audioInputManager.GetRecentAmplitudes();

                for (var i = 0; i < thresholdCalibrationSteps; i++)
                {
                    if (configurationCTS.IsCancellationRequested) return;

                    amplitudes.AddRange(audioInputManager.GetRecentAmplitudes());
                    int step = gainCalibrationSteps + i + 1;
                    await RunOnMainThread(() => SetProgressValue(step));
                    await Task.Delay(calibrationUpdateInterval, configurationCTS.Token);
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
            catch (OperationCanceledException)
            {
                // Dialog closed while calibrating.
            }
            catch (Exception ex)
            {
                Logger.client.Warning($"[AudioWizard] Calibration failed: {ex.Message}");
                try
                {
                    await RunOnMainThread(() =>
                        wizardStatusText?.SetNewText(UIUtils.I18n($"{i18nPrefix}.Status.CalibrationError")));
                }
                catch
                {
                    // Wizard may already be closed.
                }
            }
            finally
            {
                configurationInProcess = false;
            }
        }

        private void SetProgressValue(int step)
        {
            if (!IsOpened() || SingleComposer == null)
            {
                return;
            }

            var progressBar = SingleComposer.GetStatbar("progressBar");
            if (progressBar != null)
            {
                progressBar.SetValue(step);
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
            if (effectiveGains == null || effectiveGains.Count == 0)
            {
                return ModConfig.ClientConfig.InputGain;
            }

            effectiveGains.Sort();
            float lowerQuartileGain = effectiveGains[effectiveGains.Count / 4];
            float newGain = AudioUtils.FactorToDBs(lowerQuartileGain);
            newGain = GameMath.Clamp(newGain, -20, 20);
            return AudioUtils.DBsToFactor(newGain);
        }

        private float ComputeCalibratedThreshold(List<double> amplitudes)
        {
            if (amplitudes == null || amplitudes.Count == 0)
            {
                return ModConfig.ClientConfig.InputThreshold;
            }

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
            if (!IsOpened() || SingleComposer == null)
            {
                return;
            }

            var dropdown = SingleComposer.GetDropDown("inputDevice");
            dropdown?.SetSelectedValue(ModConfig.ClientConfig.InputDevice ?? "Default");
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
