using RPVoiceChat.Audio;
using System;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class AudioMeter : GuiElementStatbar, IExtendedGuiElement
    {
        private const double audioMeterWidth = 200.0;
        private ICoreClientAPI capi;
        private MicrophoneManager _audioInputManager;
        private GuiDialog parrentDialog;
        private long gameTickListenerId;
        private double coefficient;
        private double threshold;

        public AudioMeter(ICoreClientAPI capi, MicrophoneManager audioInputManager, GuiDialog parrent) : base(capi, null, new double[3] { 0.1, 0.4, 0.1 }, false, true)
        {
            this.capi = capi;
            _audioInputManager = audioInputManager;
            parrentDialog = parrent;
            coefficient = 100 / _audioInputManager.GetMaxInputThreshold();

            parrentDialog.OnOpened += OnElementShown;
            parrentDialog.OnClosed += OnElementHidden;
        }

        public void SetBounds(ElementBounds bounds)
        {
            Bounds = bounds.FlatCopy().WithFixedWidth(audioMeterWidth);
        }

        private void OnElementShown()
        {
            gameTickListenerId = capi.Event.RegisterGameTickListener(TickUpdate, 20);
        }

        private void OnElementHidden()
        {
            capi.Event.UnregisterGameTickListener(gameTickListenerId);
        }

        private void TickUpdate(float obj)
        {
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
