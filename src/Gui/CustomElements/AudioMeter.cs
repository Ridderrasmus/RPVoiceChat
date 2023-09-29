using System;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class AudioMeter : GuiElementStatbar
    {
        private ICoreClientAPI capi;

        private double coefficient;
        private double threshold;

        public AudioMeter(ICoreClientAPI capi, ElementBounds elementBounds) : base(capi, elementBounds, new double[3] { 0.1, 0.4, 0.1 }, false, true)
        {
            this.capi = capi;
        }

        public void SetCoefficient(double coef)
        {
            coefficient = coef;
        }

        public void SetThreshold(double threshold)
        {
            this.threshold = threshold;
        }

        public void UpdateVisuals(double amplitude)
        {
            if (amplitude <= 0) amplitude = 0;

            ShouldFlash = amplitude > threshold;
            amplitude = amplitude * coefficient;
            amplitude = Math.Round(amplitude);

            SetValue((float)amplitude);
        }
    }
}
