using RPVoiceChat.Config;
using RPVoiceChat.GameContent.BlockEntity;
using System;
using System.Collections.Generic;
using System.Reflection;
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
        private GuiElementDropDown targetDropDownElem;
        private GuiElementTextInput endpointNameInputElem;
        private string pendingEndpointName = "";
        private bool? lastCanEditRouting;

        public TelegraphMenuDialog(ICoreClientAPI capi, BlockEntityTelegraph telegraphBlock) : base(capi)
        {
            this.telegraphBlock = telegraphBlock;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            ElementBounds sentTextBounds = ElementBounds.Fixed(0, 40, 420, 30);
            ElementBounds receivedTextBounds = ElementBounds.Fixed(0, 80, 420, 30);
            ElementBounds countdownTextBounds = ElementBounds.Fixed(0, 120, 420, 30);
            ElementBounds sentCountdownTextBounds = ElementBounds.Fixed(0, 160, 420, 30);
            ElementBounds endpointNameLabelBounds = ElementBounds.Fixed(0, 200, 420, 20);
            ElementBounds endpointNameInputBounds = ElementBounds.Fixed(0, 225, 310, 26);
            ElementBounds endpointNameSaveBounds = ElementBounds.Fixed(322, 225, 98, 26);
            ElementBounds endpointTargetLabelBounds = ElementBounds.Fixed(0, 260, 420, 20);
            ElementBounds endpointTargetDropdownBounds = ElementBounds.Fixed(0, 285, 420, 26);
            ElementBounds noPowerInfoBounds = ElementBounds.Fixed(0, 320, 420, 24);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            var allBounds = new List<ElementBounds> { sentTextBounds, receivedTextBounds, countdownTextBounds, sentCountdownTextBounds };
            bool managedBySwitchboard = telegraphBlock.IsManagedBySwitchboard();
            if (managedBySwitchboard)
            {
                allBounds.Add(endpointNameLabelBounds);
                allBounds.Add(endpointNameInputBounds);
                allBounds.Add(endpointNameSaveBounds);
                allBounds.Add(endpointTargetLabelBounds);
                allBounds.Add(endpointTargetDropdownBounds);
                if (!telegraphBlock.HasAdvancedRoutingEnabled())
                {
                    allBounds.Add(noPowerInfoBounds);
                }
            }
            bgBounds.WithChildren(allBounds.ToArray());

            var composer = capi.Gui.CreateCompo("telegraphmenu", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Telegraph.Gui.Title"), OnTitleBarCloseClicked)
                .AddDynamicText(UIUtils.I18n("Telegraph.Gui.Sent", ' '), CairoFont.WhiteSmallText(), sentTextBounds, key: "sentText") // TODO: not long enough / no wrapword
                .AddDynamicText(UIUtils.I18n("Telegraph.Gui.Received", ' '), CairoFont.WhiteSmallText(), receivedTextBounds, key: "receivedText") // TODO: not long enough / no wrapword
                .AddDynamicText("", CairoFont.WhiteSmallText(), countdownTextBounds, key: "countdownText")
                .AddDynamicText("", CairoFont.WhiteSmallText(), sentCountdownTextBounds, key: "sentCountdownText");

            if (managedBySwitchboard)
            {
                bool canEditRouting = telegraphBlock.HasAdvancedRoutingEnabled();

                if (canEditRouting)
                {
                    string[] endpointValues = BuildEndpointValues();
                    string[] endpointNames = BuildEndpointNames(endpointValues);
                    int selectedIndex = ResolveSelectedTargetIndex(endpointValues, telegraphBlock.GetTargetEndpointName());

                    composer = composer
                        .AddStaticText(UIUtils.I18n("Telegraph.Gui.EndpointName"), CairoFont.WhiteSmallText(), endpointNameLabelBounds)
                        .AddTextInput(endpointNameInputBounds, OnEndpointNameInputChanged, CairoFont.TextInput(), "endpointNameInput")
                        .AddSmallButton(UIUtils.I18n("Telegraph.Gui.Save"), OnSaveEndpointNameClicked, endpointNameSaveBounds)
                        .AddStaticText(UIUtils.I18n("Telegraph.Gui.Target"), CairoFont.WhiteSmallText(), endpointTargetLabelBounds)
                        .AddDropDown(endpointValues, endpointNames, selectedIndex, OnTargetSelected, endpointTargetDropdownBounds, "endpointTargetDropdown");
                }
                else
                {
                    composer = composer
                        .AddStaticText(UIUtils.I18n("Telegraph.Gui.EndpointReadOnly", telegraphBlock.GetCustomEndpointName()), CairoFont.WhiteSmallText(), endpointNameInputBounds)
                        .AddStaticText(UIUtils.I18n("Telegraph.Gui.TargetReadOnly", UIUtils.I18n("Telegraph.Gui.TargetAll")), CairoFont.WhiteSmallText(), endpointTargetLabelBounds)
                        .AddStaticText(UIUtils.I18n("Telegraph.Gui.SwitchboardNoPower"), CairoFont.WhiteSmallText(), noPowerInfoBounds);
                }
            }

            SingleComposer = composer.Compose();

            sentTextElem = SingleComposer.GetDynamicText("sentText");
            receivedTextElem = SingleComposer.GetDynamicText("receivedText");
            countdownTextElem = SingleComposer.GetDynamicText("countdownText");
            sentCountdownTextElem = SingleComposer.GetDynamicText("sentCountdownText");

            UpdateSentText(telegraphBlock.GetSentMessage());
            UpdateReceivedText(telegraphBlock.GetReceivedMessage());
            RefreshRoutingControls();
            lastCanEditRouting = telegraphBlock.HasAdvancedRoutingEnabled();
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

            if (IsEndpointInputFocused())
            {
                if (!telegraphBlock.HasAdvancedRoutingEnabled())
                {
                    return;
                }

                HandleEndpointInputKey(args);
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
        
        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
            if (!telegraphBlock.IsManagedBySwitchboard())
            {
                return;
            }

            bool canEdit = telegraphBlock.HasAdvancedRoutingEnabled();
            if (lastCanEditRouting == null)
            {
                lastCanEditRouting = canEdit;
                return;
            }

            // Keep behavior simple and stable: if power drops while UI is open, close dialog.
            if (lastCanEditRouting.Value && !canEdit)
            {
                TryClose();
                return;
            }

            lastCanEditRouting = canEdit;
        }

        public void RefreshRoutingControls()
        {
            if (SingleComposer == null || !telegraphBlock.IsManagedBySwitchboard()) return;

            // In no-power mode, these interactive controls are intentionally absent.
            // Use safe element lookup to avoid exceptions when opening the GUI.
            endpointNameInputElem = SingleComposer.GetElement("endpointNameInput") as GuiElementTextInput;
            targetDropDownElem = SingleComposer.GetElement("endpointTargetDropdown") as GuiElementDropDown;

            bool canEditRouting = telegraphBlock.HasAdvancedRoutingEnabled();
            if (endpointNameInputElem == null || targetDropDownElem == null)
            {
                return;
            }

            pendingEndpointName = telegraphBlock.GetCustomEndpointName();

            endpointNameInputElem?.SetValue(pendingEndpointName);
            endpointNameInputElem?.SetPlaceHolderText(UIUtils.I18n("Telegraph.Gui.EndpointNamePlaceholder"));

            if (canEditRouting)
            {
                string[] endpointValues = BuildEndpointValues();
                string[] endpointNames = BuildEndpointNames(endpointValues);
                int selectedIndex = ResolveSelectedTargetIndex(endpointValues, telegraphBlock.GetTargetEndpointName());
                targetDropDownElem.SetList(endpointValues, endpointNames);
                targetDropDownElem.SetSelectedIndex(selectedIndex);
            }
            else
            {
                string[] endpointValues = new[] { "all" };
                string[] endpointNames = new[] { UIUtils.I18n("Telegraph.Gui.TargetAll") };
                targetDropDownElem.SetList(endpointValues, endpointNames);
                targetDropDownElem.SetSelectedIndex(0);
            }
        }

        private void OnEndpointNameInputChanged(string value)
        {
            if (!telegraphBlock.HasAdvancedRoutingEnabled())
            {
                endpointNameInputElem?.SetValue(telegraphBlock.GetCustomEndpointName());
                return;
            }
            pendingEndpointName = value ?? "";
        }

        private bool OnSaveEndpointNameClicked()
        {
            if (!telegraphBlock.HasAdvancedRoutingEnabled())
            {
                capi?.TriggerChatMessage(UIUtils.I18n("Telegraph.Settings.DisabledNoPower"));
                return true;
            }

            string candidate = (pendingEndpointName ?? "").Trim();
            if (candidate.Length > 0)
            {
                foreach (string existing in telegraphBlock.GetAvailableEndpointNames())
                {
                    if (string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        capi?.TriggerChatMessage(UIUtils.I18n("Telegraph.Settings.NameAlreadyUsed"));
                        return true;
                    }
                }
            }

            telegraphBlock.RequestSaveCustomEndpointName(pendingEndpointName);
            TryUnfocusEndpointInput();
            return true;
        }

        private void OnTargetSelected(string value, bool selected)
        {
            if (!selected) return;
            if (!telegraphBlock.HasAdvancedRoutingEnabled())
            {
                targetDropDownElem?.SetSelectedIndex(0);
                return;
            }
            telegraphBlock.RequestTargetEndpointChange(value);
        }

        private string[] BuildEndpointValues()
        {
            string[] names = telegraphBlock.GetAvailableEndpointNames();
            string[] values = new string[names.Length + 1];
            values[0] = "all";
            Array.Copy(names, 0, values, 1, names.Length);
            return values;
        }

        private string[] BuildEndpointNames(string[] values)
        {
            string[] names = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                names[i] = values[i] == "all" ? UIUtils.I18n("Telegraph.Gui.TargetAll") : values[i];
            }

            return names;
        }

        private static int ResolveSelectedTargetIndex(string[] values, string selectedValue)
        {
            if (string.IsNullOrWhiteSpace(selectedValue)) return 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], selectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }

        private bool IsEndpointInputFocused()
        {
            if (endpointNameInputElem == null) return false;

            // API compatibility across VS versions: check common focus members by reflection.
            Type type = endpointNameInputElem.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                foreach (string propName in new[] { "HasFocus", "IsFocused", "Focused" })
                {
                    PropertyInfo prop = type.GetProperty(propName, flags);
                    if (prop != null && prop.PropertyType == typeof(bool))
                    {
                        if ((bool)prop.GetValue(endpointNameInputElem)) return true;
                    }
                }

                foreach (string methodName in new[] { "HasFocus", "IsFocused", "Focused" })
                {
                    MethodInfo method = type.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                    if (method != null && method.ReturnType == typeof(bool))
                    {
                        if ((bool)method.Invoke(endpointNameInputElem, null)) return true;
                    }
                }
            }
            catch
            {
                // Ignore reflection API mismatch and fallback to non-focused behavior.
            }

            return false;
        }

        private void TryUnfocusEndpointInput()
        {
            if (endpointNameInputElem == null) return;

            Type type = endpointNameInputElem.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (string methodName in new[] { "Unfocus", "ClearFocus", "Blur" })
            {
                MethodInfo method = type.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    method.Invoke(endpointNameInputElem, null);
                    return;
                }
            }

            foreach (string methodName in new[] { "SetFocus", "SetFocused", "Focus" })
            {
                MethodInfo method = type.GetMethod(methodName, flags, null, new[] { typeof(bool) }, null);
                if (method != null)
                {
                    method.Invoke(endpointNameInputElem, new object[] { false });
                    return;
                }
            }

            foreach (string propName in new[] { "HasFocus", "IsFocused", "Focused" })
            {
                PropertyInfo prop = type.GetProperty(propName, flags);
                if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
                {
                    prop.SetValue(endpointNameInputElem, false);
                    return;
                }
            }
        }

        private bool TryForwardKeyToEndpointInput(KeyEvent args)
        {
            if (endpointNameInputElem == null) return false;

            Type type = endpointNameInputElem.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                MethodInfo onKeyPress = type.GetMethod("OnKeyPress", flags, null, new[] { typeof(KeyEvent) }, null);
                if (onKeyPress != null)
                {
                    object result = onKeyPress.Invoke(endpointNameInputElem, new object[] { args });
                    if (result is bool consumed)
                    {
                        return consumed;
                    }
                    return true;
                }
            }
            catch
            {
                // Reflection dispatch can fail depending on API version; treat as not forwarded.
            }

            return false;
        }

        private void HandleEndpointInputKey(KeyEvent args)
        {
            if (endpointNameInputElem == null) return;

            if (args.KeyCode == (int)GlKeys.BackSpace)
            {
                if (!string.IsNullOrEmpty(pendingEndpointName))
                {
                    pendingEndpointName = pendingEndpointName.Substring(0, pendingEndpointName.Length - 1);
                    endpointNameInputElem.SetValue(pendingEndpointName);
                }
                return;
            }

            if (TryForwardKeyToEndpointInput(args))
            {
                return;
            }

            if (args.KeyChar != '\0' && !char.IsControl(args.KeyChar))
            {
                pendingEndpointName += args.KeyChar;
                endpointNameInputElem.SetValue(pendingEndpointName);
            }
        }
    }
}
