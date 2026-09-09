using System;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace RPVoiceChat.Gui
{
    public class Announce : HudElement
    {
        private string title;
        private string message;
        private double displayDuration;
        private double startTime;
        private bool isDisplaying = false;
        private bool showBackground = true;

        public Announce(ICoreClientAPI capi) : base(capi)
        {
            // Initialize with empty bounds, will be recreated in SetupDialog
        }

        public void ShowAnnouncement(string title, string message, double durationSeconds = 5.0, bool showBackground = true)
        {
            // Execute on main thread to avoid crashes from network thread
            capi.Event.EnqueueMainThreadTask(() =>
            {
                try
                {
                    ShowAnnouncementInternal(title, message, durationSeconds, showBackground);
                }
                catch (Exception e)
                {
                    Logger.client.Error($"Error displaying announcement: {e}");
                }
            }, "rpvc-announce");
        }

        private void ShowAnnouncementInternal(string title, string message, double durationSeconds, bool showBackground)
        {
            this.title = title ?? "";
            this.message = message ?? "";
            this.displayDuration = durationSeconds;
            this.showBackground = showBackground;
            this.startTime = capi.World.ElapsedMilliseconds / 1000.0;
            this.isDisplaying = true;

            // Close the old dialog if it's open
            if (IsOpened())
            {
                TryClose();
            }

            SetupDialog();
            TryOpen();
        }

        private void SetupDialog()
        {
            var titleFont = CairoFont.WhiteMediumText().WithFontSize(24f);
            var messageFont = CairoFont.WhiteDetailText();

            double maxWidth = 500;
            double padding = 20;
            double topPadding = showBackground ? 35 : 20; // Less padding without background

            // Simple fixed bounds approach
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, maxWidth, 220);
            ElementBounds titleBounds = ElementBounds.Fixed(padding, topPadding, maxWidth - padding * 2, 40);
            ElementBounds messageBounds = ElementBounds.Fixed(padding, topPadding + 50, maxWidth - padding * 2, 100);

            // Dialog bounds - centered on screen
            ElementBounds dialogBounds = ElementBounds.Fixed(
                (capi.Render.FrameWidth - maxWidth) / 2,
                capi.Render.FrameHeight / 4,
                maxWidth,
                220
            );

            var composer = capi.Gui.CreateCompo("rpvcannounce", dialogBounds);
            
            if (showBackground)
            {
                // Full opaque background
                composer.AddShadedDialogBG(bgBounds, true);
            }
            else
            {
                // Semi-transparent dark background
                composer.AddInset(bgBounds, 3, 0.5f);
            }
            
            SingleComposer = composer
                .AddStaticText(title, titleFont, EnumTextOrientation.Center, titleBounds, "titleText")
                .AddStaticText(message, messageFont, messageBounds, "messageText")
                .Compose();
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            if (isDisplaying)
            {
                double currentTime = capi.World.ElapsedMilliseconds / 1000.0;
                double elapsed = currentTime - startTime;

                if (elapsed >= displayDuration)
                {
                    isDisplaying = false;
                    TryClose();
                }
            }
        }

        public override string ToggleKeyCombinationCode => null;

        public override EnumDialogType DialogType => EnumDialogType.HUD;
    }
}
