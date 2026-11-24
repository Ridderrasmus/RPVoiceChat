using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    /// <summary>
    /// Draws semi-transparent item icon when slot is empty (call from dialog OnRenderGUI after base).
    /// </summary>
    public static class AllowedItemGhostDraw
    {
        public const int DefaultGhostColor = 0x55FFFFFF;
        public const float SlotContentSize = 48f;

        /// <summary>Center ghost inside the given rectangle; optional pixel nudge.</summary>
        public static void DrawInRect(
            ICoreClientAPI capi,
            ItemSlot watchSlot,
            ItemStack ghostStack,
            double rectX,
            double rectY,
            double rectW,
            double rectH,
            float size = 24f,
            int colorArgb = DefaultGhostColor,
            float offsetX = 0f,
            float offsetY = 0f)
        {
            if (ghostStack == null || watchSlot == null || !watchSlot.Empty) return;

            double x = rectX + (rectW - size) / 2 + offsetX;
            double y = rectY + (rectH - size) / 2 + offsetY;

#pragma warning disable CS0618
            capi.Render.RenderItemstackToGui(ghostStack, x, y, 100, size, colorArgb, false, false, false);
#pragma warning restore CS0618
        }
    }
}
