using RPVoiceChat.Config;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class TelegraphMenuDialog : GuiDialog
    {
        private BlockEntityTelegraph telegraphBlock;

        // For anti-spam: time of last sending
        private long lastKeySentMs = 0;
        private int MinDelayBetweenKeysMs => ServerConfigManager.TelegraphMinDelayBetweenKeysMs; // ms between two keystrokes max

        // Display fields for sent/received text
        private GuiElementDynamicText sentTextElem;
        private GuiElementDynamicText receivedTextElem;
        private GuiElementDynamicText countdownTextElem;
        private GuiElementDynamicText sentCountdownTextElem;

        public TelegraphMenuDialog(ICoreClientAPI capi, BlockEntityTelegraph telegraphBlock) : base(capi)
        {
            this.telegraphBlock = telegraphBlock;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            ElementBounds sentTextBounds = ElementBounds.Fixed(0, 40, 360, 30);
            ElementBounds receivedTextBounds = ElementBounds.Fixed(0, 80, 360, 30);
            ElementBounds countdownTextBounds = ElementBounds.Fixed(0, 120, 360, 30);
            ElementBounds sentCountdownTextBounds = ElementBounds.Fixed(0, 160, 360, 30);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(sentTextBounds, receivedTextBounds, countdownTextBounds, sentCountdownTextBounds); // Include texts and countdowns

            SingleComposer = capi.Gui.CreateCompo("telegraphmenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Telegraph.Gui.Title"), OnTitleBarCloseClicked)
                .AddDynamicText(UIUtils.I18n("Telegraph.Gui.Sent", ' '), CairoFont.WhiteSmallText(), sentTextBounds, key: "sentText") // TODO: not long enough / no wrapword
                .AddDynamicText(UIUtils.I18n("Telegraph.Gui.Received", ' '), CairoFont.WhiteSmallText(), receivedTextBounds, key: "receivedText") // TODO: not long enough / no wrapword
                .AddDynamicText("", CairoFont.WhiteSmallText(), countdownTextBounds, key: "countdownText")
                .AddDynamicText("", CairoFont.WhiteSmallText(), sentCountdownTextBounds, key: "sentCountdownText")
                .Compose();

            sentTextElem = SingleComposer.GetDynamicText("sentText");
            receivedTextElem = SingleComposer.GetDynamicText("receivedText");
            countdownTextElem = SingleComposer.GetDynamicText("countdownText");
            sentCountdownTextElem = SingleComposer.GetDynamicText("sentCountdownText");

            UpdateSentText(telegraphBlock.GetSentMessage());
            UpdateReceivedText(telegraphBlock.GetReceivedMessage());
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }


        public void UpdateSentText(string text)
        {
            sentTextElem?.SetNewText(UIUtils.I18n("Telegraph.Gui.Sent", text));
        }

        public void UpdateReceivedText(string text)
        {
            receivedTextElem?.SetNewText(UIUtils.I18n("Telegraph.Gui.Received", text));
        }

        public void UpdateCountdown(int seconds)
        {
            if (seconds > 0)
            {
                countdownTextElem?.SetNewText(UIUtils.I18n("Telegraph.Gui.ReceivedCountdown", seconds));
            }
            else
            {
                countdownTextElem?.SetNewText("");
            }
        }

        public void UpdateSentCountdown(int seconds)
        {
            if (seconds > 0)
            {
                sentCountdownTextElem?.SetNewText(UIUtils.I18n("Telegraph.Gui.SentCountdown", seconds));
            }
            else
            {
                sentCountdownTextElem?.SetNewText("");
            }
        }

        public override void OnKeyPress(KeyEvent args)
        {
            if (args.KeyCode == (int)GlKeys.Escape)
            {
                TryClose();
                return;
            }

            if (telegraphBlock.IsPlaying)
                return;

            long nowMs = capi.World.ElapsedMilliseconds;
            if (nowMs - lastKeySentMs < MinDelayBetweenKeysMs)
                return;

            if (args.KeyChar != '\0')
            {
                telegraphBlock.SendSignal(args.KeyChar);
                lastKeySentMs = nowMs;
            }
        }

        public override bool CaptureAllInputs() => true;
        public override string ToggleKeyCombinationCode => null;
    }
}
