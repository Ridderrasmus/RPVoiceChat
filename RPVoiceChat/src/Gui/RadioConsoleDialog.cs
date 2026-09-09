using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class RadioConsoleDialog : GuiDialogBlockEntity
    {
        private readonly BlockEntityRadioSupervisionConsole console;
        private GuiElementTextInput frequencyInput;
        private GuiElementTextInput displayNameInput;
        private string pendingFrequency = "";
        private string pendingDisplayName = "";

        public RadioConsoleDialog(ICoreClientAPI capi, BlockEntityRadioSupervisionConsole console)
            : base(UIUtils.I18n("Radio.Console.Gui.Title"), console.Pos, capi)
        {
            this.console = console;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            BuildComposer();
            pendingFrequency = console.Frequency;
            pendingDisplayName = console.DisplayName;
            frequencyInput?.SetValue(pendingFrequency);
            displayNameInput?.SetValue(pendingDisplayName);
            RefreshData();
        }

        public void RefreshData()
        {
            pendingFrequency = console.Frequency;
            pendingDisplayName = console.DisplayName;
            frequencyInput?.SetValue(pendingFrequency);
            displayNameInput?.SetValue(pendingDisplayName);
        }

        private void BuildComposer()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds frequencyLabelBounds = ElementBounds.Fixed(0, 35, 420, 18);
            ElementBounds frequencyInputBounds = ElementBounds.Fixed(0, 55, 420, 26);
            ElementBounds displayNameLabelBounds = ElementBounds.Fixed(0, 91, 420, 18);
            ElementBounds displayNameInputBounds = ElementBounds.Fixed(0, 111, 420, 26);
            ElementBounds saveBounds = ElementBounds.Fixed(0, 147, 120, 28);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(
                frequencyLabelBounds,
                frequencyInputBounds,
                displayNameLabelBounds,
                displayNameInputBounds,
                saveBounds);

            SingleComposer = capi.Gui.CreateCompo("radioconsole", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Radio.Console.Gui.Title"), OnTitleBarCloseClicked)
                .AddStaticText(UIUtils.I18n("Radio.Console.Gui.Frequency"), CairoFont.WhiteSmallText(), frequencyLabelBounds)
                .AddTextInput(frequencyInputBounds, OnFrequencyChanged, CairoFont.TextInput(), "radioFrequencyInput")
                .AddStaticText(UIUtils.I18n("Radio.Console.Gui.DisplayName"), CairoFont.WhiteSmallText(), displayNameLabelBounds)
                .AddTextInput(displayNameInputBounds, OnDisplayNameChanged, CairoFont.TextInput(), "radioDisplayNameInput")
                .AddSmallButton(UIUtils.I18n("Radio.Gui.Save"), OnSaveClicked, saveBounds)
                .Compose();

            frequencyInput = SingleComposer.GetTextInput("radioFrequencyInput");
            displayNameInput = SingleComposer.GetTextInput("radioDisplayNameInput");
        }

        private void OnFrequencyChanged(string value) => pendingFrequency = value ?? "";
        private void OnDisplayNameChanged(string value) => pendingDisplayName = value ?? "";

        private bool OnSaveClicked()
        {
            console.RequestSaveSettings(pendingFrequency, pendingDisplayName);
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
}
