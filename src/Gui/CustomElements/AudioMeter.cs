using RPVoiceChat.Audio;
using System;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class AudioMeter : GuiElementStatbar, IExtendedGuiElement
    {
        private const double audioMeterWidth = 200.0;
        private MicrophoneManager _audioInputManager;
        private GuiDialog parrentDialog;
        private bool shouldScale;
        private string key;
        private long gameTickListenerId;
        private double coefficient;
        private double threshold;

        public AudioMeter(ICoreClientAPI capi, MicrophoneManager audioInputManager, GuiDialog parrent, bool unscaled = false) : base(capi, null, new double[3] { 0.1, 0.4, 0.1 }, false, true)
        {
            _audioInputManager = audioInputManager;
            parrentDialog = parrent;
            shouldScale = !unscaled;
            SetCoefficient();

            parrentDialog.OnOpened += OnDialogOpen;
            parrentDialog.OnClosed += OnDialogClosed;
            ModConfig.ConfigUpdated += OnConfigUpdate;
        }

        public void Init(string elementKey, ElementBounds bounds, GuiComposer composer)
        {
            key = elementKey;
            Bounds = bounds.FlatCopy().WithFixedWidth(audioMeterWidth);
        }

        protected virtual void SetCoefficient()
        {
            coefficient = 100;
            if (shouldScale == false) return;
            coefficient /= _audioInputManager.GetMaxInputThreshold();
        }

        private void OnDialogOpen()
        {
            gameTickListenerId = api.Event.RegisterGameTickListener(TickUpdate, 20);
        }

        private void OnDialogClosed()
        {
            api.Event.UnregisterGameTickListener(gameTickListenerId);
        }

        private void OnConfigUpdate()
        {
            SetCoefficient();
        }

        private void TickUpdate(float _)
        {
            if (parrentDialog.IsOpened() == false || key == null) return;
            var element = parrentDialog.SingleComposer.GetElement(key);
            if (element == null) return;

            SetThreshold(_audioInputManager.GetInputThreshold());
            var amplitude = Math.Max(_audioInputManager.Amplitude, _audioInputManager.AmplitudeAverage);
            if (ModConfig.Config.IsMuted) amplitude = 0;
            UpdateVisuals(amplitude);
        }

        private void SetThreshold(double threshold)
        {
            this.threshold = threshold;
        }

        private void UpdateVisuals(double amplitude)
        {
            if (amplitude <= 0) amplitude = 0;

            ShouldFlash = amplitude > threshold;
            amplitude = amplitude * coefficient;
            amplitude = Math.Round(amplitude);

            SetValue((float)amplitude);
        }
    }
}
