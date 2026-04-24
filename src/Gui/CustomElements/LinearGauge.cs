using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Gui.CustomElements
{
    /// <summary>
    /// Thin wrapper around <see cref="GuiElementStatbar"/> for 0–100% fills (power, burn time, etc.).
    /// </summary>
    public sealed class LinearGauge
    {
        private const int Steps = 100;
        private readonly GuiElementStatbar bar;

        public LinearGauge(ICoreClientAPI capi, ElementBounds bounds, bool vertical = false)
        {
            bar = new GuiElementStatbar(capi, bounds, new double[3] { 0.1, 0.4, 0.1 }, false, vertical);
            bar.ShowValueOnHover = false;
            bar.SetValues(0, 0, Steps);
        }

        public GuiElementStatbar Element => bar;

        /// <summary>Sets fill from 0 (empty) to 1 (full).</summary>
        public void SetRatio01(float ratio)
        {
            int v = (int)GameMath.Clamp(ratio * Steps, 0, Steps);
            bar.SetValue(v);
        }
    }
}
