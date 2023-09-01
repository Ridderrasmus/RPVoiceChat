using System;
using Vintagestory.API.Client;

namespace RPVoiceChat
{
    public class GuiElementAudioMeter : GuiElementStatbar
    {
        private ICoreClientAPI capi;

        private double coefficient;
        private double threshold;

        public GuiElementAudioMeter(ICoreClientAPI capi, ElementBounds elementBounds) : base(capi, elementBounds, new double[3] {0.1,0.4,0.1}, false, true)
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


            if (amplitude > threshold)
            {
                this.ShouldFlash = true;
            }
            else
            {
                this.ShouldFlash = false;
            }
            amplitude = amplitude * coefficient;
            amplitude = Math.Round(amplitude);

            SetValue((float)amplitude);
        }
    }
}
