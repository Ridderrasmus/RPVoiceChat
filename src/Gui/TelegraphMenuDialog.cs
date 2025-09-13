using RPVoiceChat.GameContent;
using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class TelegraphMenuDialog : GuiDialog
    {
        private BlockEntityTelegraph telegraphBlock;

        // Pour anti-spam : temps du dernier envoi
        private long lastKeySentMs = 0;
        private const int MinDelayBetweenKeysMs = 200; // 200 ms entre deux touches max

        // Champs d'affichage du texte envoyé/reçu
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
            ElementBounds clearButtonBounds = ElementBounds.Fixed(0, 120, 100, 30);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(sentTextBounds, receivedTextBounds);

            SingleComposer = capi.Gui.CreateCompo("telegraphmenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Télégraphe", OnTitleBarCloseClicked)
                .AddDynamicText("Envoyé :", CairoFont.WhiteSmallText(), sentTextBounds, key: "sentText")
                .AddDynamicText("Reçu :", CairoFont.WhiteSmallText(), receivedTextBounds, key: "receivedText")
                .AddButton("Effacer", OnClearClicked, clearButtonBounds)
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
            sentTextElem?.SetNewText($"Envoyé : {text}");
        }

        public void UpdateReceivedText(string text)
        {
            receivedTextElem?.SetNewText($"Reçu : {text}");
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
