
using RPVoiceChat.Config;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class FirstLaunchDialog : GuiDialog
    {
        public override double DrawOrder => 0.09;
        private const string i18nPrefix = "Gui.FirstLaunchDialog";
        private const string composerName = "RPVC_FirstLaunchDialog";
        private const int textYOffset = 10;
        private const int textLeftPadding = 5;
        private const int textBottomPadding = 25;
        private const int textWidth = 460;
        private const int buttonHeight = 30;
        private const int buttonXPadding = 10;
        private const int buttonYPadding = 2;
        private GuiManager guiManager;

        public FirstLaunchDialog(ICoreClientAPI capi, GuiManager guiManager) : base(capi)
        {
            this.guiManager = guiManager;
        }

        public void ShowIfNecessary()
        {
            if (ModConfig.ClientConfig.FirstTimeUse == false) return;

            Compose();
            TryOpen();
            ModConfig.ClientConfig.FirstTimeUse = false;
            ModConfig.SaveClient(capi);
        }

        private void Compose()
        {
            var drawUtil = new TextDrawUtil();
            var font = CairoFont.WhiteSmallishText();
            var modMenuHotkey = capi.Input.GetHotKeyByCode("voicechatMenu").CurrentMapping.ToString();

            var titleBarText = UIUtils.I18n($"{i18nPrefix}.TitleBar");
            var firstTextBlock = UIUtils.I18n($"{i18nPrefix}.FirstParagraph");
            var secondTextBlock = UIUtils.I18n($"{i18nPrefix}.SecondParagraph", modMenuHotkey);
            var notNowButtonText = UIUtils.I18n($"Gui.Button.NotNow");
            var sureButtonText = UIUtils.I18n($"Gui.Button.Sure");
            var firstTextBlockHeight = drawUtil.GetMultilineTextHeight(font, firstTextBlock, textWidth);
            var secondTextBlockHeight = drawUtil.GetMultilineTextHeight(font, secondTextBlock, textWidth);

            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing(ElementSizing.FitToChildren);
            var firstTextBlockBounds = ElementBounds.Fixed(textLeftPadding, GuiStyle.TitleBarHeight + textYOffset, textWidth, firstTextBlockHeight);
            var secondTextBlockBounds = firstTextBlockBounds.BelowCopy(0, textBottomPadding).WithFixedHeight(secondTextBlockHeight);
            var buttonBounds = secondTextBlockBounds.BelowCopy(-textLeftPadding, textBottomPadding).WithFixedSize(0, buttonHeight).WithFixedPadding(buttonXPadding, buttonYPadding);

            SingleComposer = capi.Gui.CreateCompo(composerName, ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleBarText, () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddStaticText(firstTextBlock, font, firstTextBlockBounds)
                    .AddStaticText(secondTextBlock, font, secondTextBlockBounds)
                    .AddButton(notNowButtonText, TryClose, buttonBounds)
                    .AddButton(sureButtonText, OnSureButtonClick, buttonBounds.FlatCopy().WithAlignment(EnumDialogArea.RightFixed))
                .EndChildElements()
                .Compose();
        }

        private bool OnSureButtonClick()
        {
            TryClose();
            guiManager.audioWizardDialog.TryOpen();
            return true;
        }

        public override string ToggleKeyCombinationCode => null;
    }
}
