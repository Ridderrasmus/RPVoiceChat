using System;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class TelephoneMenuDialog : GuiDialog
    {
        private readonly BlockEntityTelephone telephoneBlock;
        private GuiElementTextInput numberInput;
        private GuiElementDropDown targetDropDown;
        private string pendingNumber = "";

        public TelephoneMenuDialog(ICoreClientAPI capi, BlockEntityTelephone telephoneBlock) : base(capi)
        {
            this.telephoneBlock = telephoneBlock;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds statusBounds = ElementBounds.Fixed(0, 35, 420, 18);
            ElementBounds numberLabelBounds = ElementBounds.Fixed(0, 62, 420, 18);
            ElementBounds numberInputBounds = ElementBounds.Fixed(0, 82, 320, 26);
            ElementBounds numberSaveBounds = ElementBounds.Fixed(332, 82, 88, 26);
            ElementBounds targetLabelBounds = ElementBounds.Fixed(0, 118, 420, 18);
            ElementBounds targetDropDownBounds = ElementBounds.Fixed(0, 138, 420, 26);
            ElementBounds callButtonBounds = ElementBounds.Fixed(0, 176, 420, 28);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(
                statusBounds,
                numberLabelBounds,
                numberInputBounds,
                numberSaveBounds,
                targetLabelBounds,
                targetDropDownBounds,
                callButtonBounds
            );

            string statusText = telephoneBlock.CanCompose()
                ? UIUtils.I18n("Telephone.Gui.ComposeReady")
                : UIUtils.I18n(telephoneBlock.GetComposeDisabledReasonLangKey());

            SingleComposer = capi.Gui.CreateCompo("telephonemenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Telephone.Gui.Title"), OnTitleBarCloseClicked)
                .AddStaticText(statusText, CairoFont.WhiteSmallText(), statusBounds)
                .AddStaticText(UIUtils.I18n("Telephone.Gui.Number"), CairoFont.WhiteSmallText(), numberLabelBounds)
                .AddTextInput(numberInputBounds, OnNumberInputChanged, CairoFont.TextInput(), "telephoneNumberInput")
                .AddSmallButton(UIUtils.I18n("Telephone.Gui.Save"), OnSaveNumberClicked, numberSaveBounds)
                .AddStaticText(UIUtils.I18n("Telephone.Gui.Target"), CairoFont.WhiteSmallText(), targetLabelBounds)
                .AddDropDown(new[] { "" }, new[] { UIUtils.I18n("Telephone.Gui.AutoOrNone") }, 0, OnTargetSelected, targetDropDownBounds, "telephoneTargetDropdown")
                .AddSmallButton(UIUtils.I18n("Telephone.Gui.Call"), OnCallClicked, callButtonBounds)
                .Compose();

            numberInput = SingleComposer.GetElement("telephoneNumberInput") as GuiElementTextInput;
            targetDropDown = SingleComposer.GetElement("telephoneTargetDropdown") as GuiElementDropDown;

            pendingNumber = telephoneBlock.GetPhoneNumber();
            numberInput?.SetValue(pendingNumber);
            RefreshData();
        }

        public void RefreshData()
        {
            if (SingleComposer == null) return;

            pendingNumber = telephoneBlock.GetPhoneNumber();
            numberInput?.SetValue(pendingNumber);

            string[] numbers = telephoneBlock.GetAvailableTargetNumbers();
            string[] values = new string[numbers.Length + 1];
            string[] names = new string[numbers.Length + 1];
            values[0] = "";
            names[0] = UIUtils.I18n("Telephone.Gui.AutoOrNone");
            Array.Copy(numbers, 0, values, 1, numbers.Length);
            for (int i = 0; i < numbers.Length; i++)
            {
                bool isBusy = telephoneBlock.IsTargetNumberBusy(numbers[i]);
                names[i + 1] = isBusy
                    ? $"{numbers[i]} {UIUtils.I18n("Telephone.Gui.TargetBusySuffix")}"
                    : numbers[i];
            }
            targetDropDown?.SetList(values, names);

            string currentTarget = telephoneBlock.GetTargetNumber() ?? "";
            int selected = Array.IndexOf(values, currentTarget);
            if (selected < 0) selected = 0;
            targetDropDown?.SetSelectedIndex(selected);
        }

        public override string ToggleKeyCombinationCode => null;

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }

        private void OnNumberInputChanged(string value)
        {
            if (telephoneBlock.IsManagedBySwitchboard() && !telephoneBlock.CanCompose())
            {
                numberInput?.SetValue(telephoneBlock.GetPhoneNumber());
                return;
            }

            string digits = new string((value ?? "").ToCharArray());
            pendingNumber = new string(Array.FindAll(digits.ToCharArray(), char.IsDigit));
            if (pendingNumber.Length > 6) pendingNumber = pendingNumber.Substring(0, 6);
        }

        private bool OnSaveNumberClicked()
        {
            if (telephoneBlock.IsManagedBySwitchboard() && !telephoneBlock.CanCompose())
            {
                capi?.TriggerIngameError(this, "telephone-number-disabled", UIUtils.I18n(telephoneBlock.GetComposeDisabledReasonLangKey()));
                return true;
            }

            if (telephoneBlock.IsManagedBySwitchboard())
            {
                foreach (string existing in telephoneBlock.GetAvailableTargetNumbers())
                {
                    if (string.Equals(existing, pendingNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        capi?.TriggerIngameError(this, "telephone-number-used", UIUtils.I18n("Telegraph.Settings.NameAlreadyUsed"));
                        return true;
                    }
                }
            }

            telephoneBlock.RequestSavePhoneNumber(pendingNumber);
            return true;
        }

        private void OnTargetSelected(string value, bool selected)
        {
            if (!selected) return;
            if (telephoneBlock.IsManagedBySwitchboard() && !telephoneBlock.CanCompose())
            {
                RefreshData();
                return;
            }
            telephoneBlock.RequestTargetNumberChange(value ?? "");
        }

        private bool OnCallClicked()
        {
            string failureLangKey = telephoneBlock.GetCallFailureLangKeyForUi();
            if (!string.IsNullOrWhiteSpace(failureLangKey))
            {
                capi?.TriggerIngameError(this, "telephone-call-failed", UIUtils.I18n(failureLangKey));
                return true;
            }

            telephoneBlock.RequestStartCall();
            return true;
        }
    }
}
