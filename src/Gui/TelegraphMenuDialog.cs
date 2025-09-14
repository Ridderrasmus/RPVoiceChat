using RPVoiceChat.GameContent;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using Vintagestory.API.Client;
using static System.Net.Mime.MediaTypeNames;

namespace RPVoiceChat.Gui
{
    public class TelegraphMenuDialog : GuiDialog
    {
        private BlockEntityTelegraph telegraphBlock;

        // For anti-spam: time of last sending
        private long lastKeySentMs = 0;
        private const int MinDelayBetweenKeysMs = 200; // 200 ms between two keystrokes max

        // Display fields for sent/received text
        private GuiElementDynamicText sentTextElem;
        private GuiElementDynamicText receivedTextElem;

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
            ElementBounds clearButtonBounds = ElementBounds.Fixed(130, 130, 100, 30); // Centré

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(sentTextBounds, receivedTextBounds, clearButtonBounds); // Inclure le bouton dans le fond

            SingleComposer = capi.Gui.CreateCompo("telegraphmenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Telegraph.Gui.Title"), OnTitleBarCloseClicked)
                .AddDynamicText(UIUtils.I18n("Telegraph.Gui.Sent", ' '), CairoFont.WhiteSmallText(), sentTextBounds, key: "sentText") // TODO: not long enough / no wrapword
                .AddDynamicText(UIUtils.I18n("Telegraph.Gui.Received", ' '), CairoFont.WhiteSmallText(), receivedTextBounds, key: "receivedText") // TODO: not long enough / no wrapword
                .AddButton(UIUtils.I18n("Telegraph.Gui.Delete"), OnClearClicked, clearButtonBounds)
                .Compose();

            sentTextElem = SingleComposer.GetDynamicText("sentText");
            receivedTextElem = SingleComposer.GetDynamicText("receivedText");

            UpdateSentText(telegraphBlock.GetSentMessage());
            UpdateReceivedText(telegraphBlock.GetReceivedMessage());
        }


        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }

        private bool OnClearClicked()
        {
            telegraphBlock.ClearMessages();
            return true;
        }

        public void UpdateSentText(string text)
        {
            sentTextElem?.SetNewText(UIUtils.I18n("Telegraph.Gui.Sent", text));
        }

        public void UpdateReceivedText(string text)
        {
            receivedTextElem?.SetNewText(UIUtils.I18n("Telegraph.Gui.Received", text));
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
