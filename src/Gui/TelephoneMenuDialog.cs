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
        private GuiElementDynamicText statusTextElem;
        private GuiElementDynamicText dialNumberTextElem;
        private GuiElement actionButtonElem;
        private string pendingNumber = "";
        private string pendingDialNumber = "";
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
            pendingDialNumber = telephoneBlock.GetTargetNumber() ?? "";
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
            ElementBounds dialDisplayBounds = ElementBounds.Fixed(0, 122, 420, 22);
            ElementBounds key1Bounds = ElementBounds.Fixed(0, 170, 132, 28);
            ElementBounds key2Bounds = ElementBounds.Fixed(144, 170, 132, 28);
            ElementBounds key3Bounds = ElementBounds.Fixed(288, 170, 132, 28);
            ElementBounds key4Bounds = ElementBounds.Fixed(0, 204, 132, 28);
            ElementBounds key5Bounds = ElementBounds.Fixed(144, 204, 132, 28);
            ElementBounds key6Bounds = ElementBounds.Fixed(288, 204, 132, 28);
            ElementBounds key7Bounds = ElementBounds.Fixed(0, 238, 132, 28);
            ElementBounds key8Bounds = ElementBounds.Fixed(144, 238, 132, 28);
            ElementBounds key9Bounds = ElementBounds.Fixed(288, 238, 132, 28);
            ElementBounds keyClearBounds = ElementBounds.Fixed(0, 272, 132, 28);
            ElementBounds key0Bounds = ElementBounds.Fixed(144, 272, 132, 28);
            ElementBounds keyBackBounds = ElementBounds.Fixed(288, 272, 132, 28);
            ElementBounds callButtonBounds = ElementBounds.Fixed(0, 308, 420, 28);

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
                    dialDisplayBounds,
                    key1Bounds,
                    key2Bounds,
                    key3Bounds,
                    key4Bounds,
                    key5Bounds,
                    key6Bounds,
                    key7Bounds,
                    key8Bounds,
                    key9Bounds,
                    keyClearBounds,
                    key0Bounds,
                    keyBackBounds,
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
                        .AddDynamicText("", CairoFont.WhiteSmallText(), dialDisplayBounds, "telephoneDialNumber")
                        .AddSmallButton("1", () => OnDialKeyPressed('1'), key1Bounds)
                        .AddSmallButton("2", () => OnDialKeyPressed('2'), key2Bounds)
                        .AddSmallButton("3", () => OnDialKeyPressed('3'), key3Bounds)
                        .AddSmallButton("4", () => OnDialKeyPressed('4'), key4Bounds)
                        .AddSmallButton("5", () => OnDialKeyPressed('5'), key5Bounds)
                        .AddSmallButton("6", () => OnDialKeyPressed('6'), key6Bounds)
                        .AddSmallButton("7", () => OnDialKeyPressed('7'), key7Bounds)
                        .AddSmallButton("8", () => OnDialKeyPressed('8'), key8Bounds)
                        .AddSmallButton("9", () => OnDialKeyPressed('9'), key9Bounds)
                        .AddSmallButton(UIUtils.I18n("Telephone.Gui.Clear"), OnDialClearClicked, keyClearBounds)
                        .AddSmallButton("0", () => OnDialKeyPressed('0'), key0Bounds)
                        .AddSmallButton(UIUtils.I18n("Telephone.Gui.Backspace"), OnDialBackspaceClicked, keyBackBounds);
                }
                else
                {
                    string ownNumber = telephoneBlock.GetPhoneNumber();
                    composer = composer.AddStaticText(UIUtils.I18n("Telephone.Gui.NetworkUnavailable"), CairoFont.WhiteSmallText(), numberInputBounds);
                    if (!string.IsNullOrWhiteSpace(ownNumber))
                    {
                        composer = composer.AddStaticText(UIUtils.I18n("Telephone.Gui.LocalNumber", ownNumber), CairoFont.WhiteSmallText(), dialDisplayBounds);
                    }
                }
            }

            // Always bind the same click handler so transitions (Call <-> Hang up) keep working.
            // Waiting state is still guarded by OnCallClicked and visual enabled-state toggling.
            composer.AddSmallButton(UIUtils.I18n(actionButtonLangKey), OnCallClicked, callButtonBounds, key: "telephoneActionButton");

            SingleComposer = composer.Compose();

            numberInput = SingleComposer.GetElement("telephoneNumberInput") as GuiElementTextInput;
            statusTextElem = SingleComposer.GetDynamicText("telephoneStatusText");
            dialNumberTextElem = SingleComposer.GetDynamicText("telephoneDialNumber");
            actionButtonElem = SingleComposer.GetElement("telephoneActionButton");
        }

        public void RefreshData()
        {
            if (SingleComposer == null) return;

            if (!canEditManagedOptions || numberInput == null)
            {
                RefreshActionUi();
                return;
            }

            RefreshActionUi();

            pendingNumber = telephoneBlock.GetPhoneNumber();
            numberInput.SetValue(pendingNumber);
            if (string.IsNullOrWhiteSpace(pendingDialNumber))
            {
                pendingDialNumber = telephoneBlock.GetTargetNumber() ?? "";
            }
            UpdateDialDisplay();
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

        private bool OnDialKeyPressed(char digit)
        {
            if (!canEditManagedOptions || telephoneBlock.IsCallSessionActive())
            {
                return true;
            }

            if (!char.IsDigit(digit))
            {
                return true;
            }

            if ((pendingDialNumber?.Length ?? 0) >= 6)
            {
                return true;
            }

            pendingDialNumber += digit;
            UpdateDialDisplay();
            return true;
        }

        private bool OnDialBackspaceClicked()
        {
            if (!canEditManagedOptions || telephoneBlock.IsCallSessionActive())
            {
                return true;
            }

            if (string.IsNullOrEmpty(pendingDialNumber))
            {
                return true;
            }

            pendingDialNumber = pendingDialNumber.Substring(0, pendingDialNumber.Length - 1);
            UpdateDialDisplay();
            return true;
        }

        private bool OnDialClearClicked()
        {
            if (!canEditManagedOptions || telephoneBlock.IsCallSessionActive())
            {
                return true;
            }

            pendingDialNumber = "";
            UpdateDialDisplay();
            return true;
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

            if (managedBySwitchboard && canEditManagedOptions)
            {
                if (string.IsNullOrWhiteSpace(pendingDialNumber))
                {
                    capi?.TriggerIngameError(this, "telephone-call-failed", UIUtils.I18n("Telephone.Call.Failed.NoTarget"));
                    return true;
                }

                telephoneBlock.RequestStartCall(pendingDialNumber);
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

        private void UpdateDialDisplay()
        {
            dialNumberTextElem?.SetNewText(UIUtils.I18n("Telephone.Gui.DialDisplay", pendingDialNumber ?? ""));
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
            if (telephoneBlock.IsNotInService()) return "Telephone.Gui.Hangup";
            if (telephoneBlock.HasIncomingCall()) return "Telephone.Gui.Answer";
            if (telephoneBlock.IsWaitingForAnswer()) return "Telephone.Gui.Waiting";
            return "Telephone.Gui.Call";
        }

        private string ResolveStatusText()
        {
            if (telephoneBlock.IsInCall()) return UIUtils.I18n("Telephone.Gui.InCall");
            if (telephoneBlock.IsNotInService()) return UIUtils.I18n("Telephone.Gui.NotInServiceStatus");
            if (telephoneBlock.HasIncomingCall()) return UIUtils.I18n("Telephone.Gui.IncomingCall");
            if (telephoneBlock.IsWaitingForAnswer()) return UIUtils.I18n("Telephone.Gui.WaitingStatus");
            if (!managedBySwitchboard)
            {
                return UIUtils.I18n("Telephone.Gui.DirectModeReady");
            }

            if (telephoneBlock.CanCompose())
            {
                // When managed dialing pad is visible, the UI already conveys readiness.
                return "";
            }

            return UIUtils.I18n(telephoneBlock.GetComposeDisabledReasonLangKey());
        }

    }
}
