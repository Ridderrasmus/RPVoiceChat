using System;
using System.Reflection;
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
        private GuiElementDynamicText statusTextElem;
        private GuiElement actionButtonElem;
        private string pendingNumber = "";
        private string actionButtonLangKey = "Telephone.Gui.Call";
        private bool actionButtonInteractive = true;
        private bool managedBySwitchboard;
        private bool canEditManagedOptions;

        public TelephoneMenuDialog(ICoreClientAPI capi, BlockEntityTelephone telephoneBlock) : base(capi)
        {
            this.telephoneBlock = telephoneBlock;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            BuildComposer();
            pendingNumber = telephoneBlock.GetPhoneNumber();
            numberInput?.SetValue(pendingNumber);
            RefreshData();
        }

        private void BuildComposer()
        {
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
            managedBySwitchboard = telephoneBlock.IsManagedBySwitchboard();
            canEditManagedOptions = managedBySwitchboard && telephoneBlock.CanCompose();
            if (managedBySwitchboard)
            {
                bgBounds.WithChildren(
                    statusBounds,
                    numberLabelBounds,
                    numberInputBounds,
                    numberSaveBounds,
                    targetLabelBounds,
                    targetDropDownBounds,
                    callButtonBounds
                );
            }
            else
            {
                bgBounds.WithChildren(
                    statusBounds,
                    callButtonBounds
                );
            }

            actionButtonLangKey = ResolveActionButtonLangKey();
            actionButtonInteractive = !telephoneBlock.IsWaitingForAnswer();

            var composer = capi.Gui.CreateCompo("telephonemenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Telephone.Gui.Title"), OnTitleBarCloseClicked)
                .AddDynamicText(ResolveStatusText(), CairoFont.WhiteSmallText(), statusBounds, "telephoneStatusText");

            if (managedBySwitchboard)
            {
                if (canEditManagedOptions)
                {
                    composer = composer
                        .AddStaticText(UIUtils.I18n("Telephone.Gui.Number"), CairoFont.WhiteSmallText(), numberLabelBounds)
                        .AddTextInput(numberInputBounds, OnNumberInputChanged, CairoFont.TextInput(), "telephoneNumberInput")
                        .AddSmallButton(UIUtils.I18n("Telephone.Gui.Save"), OnSaveNumberClicked, numberSaveBounds)
                        .AddStaticText(UIUtils.I18n("Telephone.Gui.Target"), CairoFont.WhiteSmallText(), targetLabelBounds)
                        .AddDropDown(new[] { "" }, new[] { UIUtils.I18n("Telephone.Gui.AutoOrNone") }, 0, OnTargetSelected, targetDropDownBounds, "telephoneTargetDropdown");
                }
                else
                {
                    composer = composer
                        .AddStaticText(UIUtils.I18n("Telephone.Gui.NumberReadOnly", telephoneBlock.GetPhoneNumber()), CairoFont.WhiteSmallText(), numberInputBounds)
                        .AddStaticText(UIUtils.I18n("Telephone.Gui.TargetReadOnly", telephoneBlock.GetTargetNumber()), CairoFont.WhiteSmallText(), targetDropDownBounds);
                }
            }

            // Always bind the same click handler so transitions (Call <-> Hang up) keep working.
            // Waiting state is still guarded by OnCallClicked and visual enabled-state toggling.
            composer.AddSmallButton(UIUtils.I18n(actionButtonLangKey), OnCallClicked, callButtonBounds, key: "telephoneActionButton");

            SingleComposer = composer.Compose();

            numberInput = SingleComposer.GetElement("telephoneNumberInput") as GuiElementTextInput;
            targetDropDown = SingleComposer.GetElement("telephoneTargetDropdown") as GuiElementDropDown;
            statusTextElem = SingleComposer.GetDynamicText("telephoneStatusText");
            actionButtonElem = SingleComposer.GetElement("telephoneActionButton");
        }

        public void RefreshData()
        {
            if (SingleComposer == null) return;

            if (!canEditManagedOptions || numberInput == null || targetDropDown == null)
            {
                RefreshActionUi();
                return;
            }

            RefreshActionUi();

            pendingNumber = telephoneBlock.GetPhoneNumber();
            numberInput.SetValue(pendingNumber);

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
            targetDropDown.SetList(values, names);

            string currentTarget = telephoneBlock.GetTargetNumber() ?? "";
            int selected = Array.IndexOf(values, currentTarget);
            if (selected < 0) selected = 0;
            targetDropDown.SetSelectedIndex(selected);
        }

        private void RefreshActionUi()
        {
            statusTextElem?.SetNewText(ResolveStatusText());

            string desiredButton = ResolveActionButtonLangKey();
            bool shouldBeInteractive = !telephoneBlock.IsWaitingForAnswer();
            if (desiredButton != actionButtonLangKey)
            {
                actionButtonLangKey = desiredButton;
                TrySetActionButtonText(UIUtils.I18n(actionButtonLangKey));
            }

            if (shouldBeInteractive != actionButtonInteractive)
            {
                actionButtonInteractive = shouldBeInteractive;
                TrySetActionButtonEnabled(actionButtonInteractive);
            }
        }

        public override string ToggleKeyCombinationCode => null;

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            if (telephoneBlock.IsCallSessionActive())
            {
                telephoneBlock.RequestEndCall();
            }
        }

        private void OnNumberInputChanged(string value)
        {
            if (!canEditManagedOptions)
            {
                return;
            }

            if (telephoneBlock.IsCallSessionActive())
            {
                // Never call SetValue from inside the text-changed callback.
                // Doing so can recurse through the same callback path and overflow the stack.
                return;
            }

            string digits = new string((value ?? "").ToCharArray());
            pendingNumber = new string(Array.FindAll(digits.ToCharArray(), char.IsDigit));
            if (pendingNumber.Length > 6) pendingNumber = pendingNumber.Substring(0, 6);
        }

        private bool OnSaveNumberClicked()
        {
            if (!canEditManagedOptions)
            {
                return true;
            }

            if (telephoneBlock.IsCallSessionActive())
            {
                return true;
            }
            foreach (string existing in telephoneBlock.GetAvailableTargetNumbers())
            {
                if (string.Equals(existing, pendingNumber, StringComparison.OrdinalIgnoreCase))
                {
                    capi?.TriggerIngameError(this, "telephone-number-used", UIUtils.I18n("Telegraph.Settings.NameAlreadyUsed"));
                    return true;
                }
            }

            telephoneBlock.RequestSavePhoneNumber(pendingNumber);
            return true;
        }

        private void OnTargetSelected(string value, bool selected)
        {
            if (!selected) return;
            if (!canEditManagedOptions)
            {
                return;
            }
            if (telephoneBlock.IsCallSessionActive())
            {
                RefreshData();
                return;
            }
            telephoneBlock.RequestTargetNumberChange(value ?? "");
        }

        private bool OnCallClicked()
        {
            if (telephoneBlock.IsWaitingForAnswer())
            {
                return true;
            }

            if (telephoneBlock.IsInCall())
            {
                telephoneBlock.RequestEndCall();
                return true;
            }

            if (telephoneBlock.HasIncomingCall())
            {
                telephoneBlock.RequestStartCall();
                return true;
            }

            string failureLangKey = telephoneBlock.GetCallFailureLangKeyForUi();
            if (!string.IsNullOrWhiteSpace(failureLangKey))
            {
                capi?.TriggerIngameError(this, "telephone-call-failed", UIUtils.I18n(failureLangKey));
                return true;
            }

            telephoneBlock.RequestStartCall();
            return true;
        }

        private void TrySetActionButtonText(string text)
        {
            if (actionButtonElem == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            Type type = actionButtonElem.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                MethodInfo setNewText = type.GetMethod("SetNewText", flags, null, new[] { typeof(string) }, null);
                if (setNewText != null)
                {
                    setNewText.Invoke(actionButtonElem, new object[] { text });
                    return;
                }

                MethodInfo setText = type.GetMethod("SetText", flags, null, new[] { typeof(string) }, null);
                if (setText != null)
                {
                    setText.Invoke(actionButtonElem, new object[] { text });
                }
            }
            catch
            {
                // Ignore API differences; button remains functional even without runtime label update.
            }
        }

        private void TrySetActionButtonEnabled(bool enabled)
        {
            if (actionButtonElem == null)
            {
                return;
            }

            Type type = actionButtonElem.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                PropertyInfo enabledProp = type.GetProperty("Enabled", flags);
                if (enabledProp != null && enabledProp.PropertyType == typeof(bool) && enabledProp.CanWrite)
                {
                    enabledProp.SetValue(actionButtonElem, enabled);
                    return;
                }

                FieldInfo enabledField = type.GetField("Enabled", flags);
                if (enabledField != null && enabledField.FieldType == typeof(bool))
                {
                    enabledField.SetValue(actionButtonElem, enabled);
                }
            }
            catch
            {
                // Ignore API differences; click handler still guards state transitions.
            }
        }

        private string ResolveActionButtonLangKey()
        {
            if (telephoneBlock.IsInCall()) return "Telephone.Gui.Hangup";
            if (telephoneBlock.HasIncomingCall()) return "Telephone.Gui.Answer";
            if (telephoneBlock.IsWaitingForAnswer()) return "Telephone.Gui.Waiting";
            return "Telephone.Gui.Call";
        }

        private string ResolveStatusText()
        {
            if (telephoneBlock.IsInCall()) return UIUtils.I18n("Telephone.Gui.InCall");
            if (telephoneBlock.HasIncomingCall()) return UIUtils.I18n("Telephone.Gui.IncomingCall");
            if (telephoneBlock.IsWaitingForAnswer()) return UIUtils.I18n("Telephone.Gui.WaitingStatus");
            if (!managedBySwitchboard)
            {
                return UIUtils.I18n("Telephone.Gui.DirectModeReady");
            }

            return telephoneBlock.CanCompose()
                ? UIUtils.I18n("Telephone.Gui.ComposeReady")
                : UIUtils.I18n(telephoneBlock.GetComposeDisabledReasonLangKey());
        }

    }
}
