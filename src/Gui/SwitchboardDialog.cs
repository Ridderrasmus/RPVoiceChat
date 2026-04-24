using RPVoiceChat.GameContent.BlockEntity;

using RPVoiceChat.Gui.CustomElements;

using RPVoiceChat.Systems;

using RPVoiceChat.Util;
using System;


using Vintagestory.API.Client;

using Vintagestory.API.Config;



namespace RPVoiceChat.Gui

{

    public class GuiDialogSwitchboard : GuiDialog

    {

        private readonly BlockEntitySwitchboard switchboard;

        private GuiElementTextInput networkNameInput;


        private GuiElementDynamicText powerPercentText;

        private LinearGauge powerGauge;

        private string pendingNetworkName = "";

        private string lastKnownServerCustomName = "";



        public GuiDialogSwitchboard(ICoreClientAPI capi, BlockEntitySwitchboard switchboard) : base(capi)

        {

            this.switchboard = switchboard;

        }



        public override void OnGuiOpened()

        {

            base.OnGuiOpened();



            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);



            ElementBounds networkNameLabelBounds = ElementBounds.Fixed(0, 35, 420, 18);

            ElementBounds networkNameInputBounds = ElementBounds.Fixed(0, 55, 320, 26);

            ElementBounds networkNameSaveBounds = ElementBounds.Fixed(332, 55, 88, 26);

            ElementBounds powerToggleLabelBounds = ElementBounds.Fixed(0, 91, 320, 18);

            ElementBounds powerToggleBounds = ElementBounds.Fixed(332, 89, 44, 24);

            ElementBounds powerGaugeLabelBounds = ElementBounds.Fixed(0, 121, 420, 18);

            ElementBounds powerGaugeBarBounds = ElementBounds.Fixed(0, 143, 420, 22);

            ElementBounds powerPercentBounds = ElementBounds.Fixed(0, 168, 420, 20);



            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);

            bgBounds.BothSizing = ElementSizing.FitToChildren;

            bgBounds.WithChildren(



                networkNameLabelBounds,

                networkNameInputBounds,

                networkNameSaveBounds,

                powerToggleLabelBounds,

                powerToggleBounds,

                powerGaugeLabelBounds,

                powerGaugeBarBounds,

                powerPercentBounds

            );



            powerGauge = new LinearGauge(capi, powerGaugeBarBounds, false);






            SingleComposer = capi.Gui.CreateCompo("switchboarddialog", dialogBounds)

                .AddShadedDialogBG(bgBounds)

                .AddDialogTitleBar(UIUtils.I18n("Switchboard.Gui.Title"), OnTitleBarCloseClicked)



                .AddStaticText(UIUtils.I18n("Switchboard.Gui.NetworkName"), CairoFont.WhiteSmallText(), networkNameLabelBounds)

                .AddTextInput(networkNameInputBounds, OnNetworkNameInputChanged, CairoFont.TextInput(), "networkNameInput")

                .AddSmallButton(UIUtils.I18n("Switchboard.Gui.Save"), OnSaveNetworkNameClicked, networkNameSaveBounds)

                .AddStaticText(UIUtils.I18n("Switchboard.Gui.UsePower"), CairoFont.WhiteSmallText(), powerToggleLabelBounds)

                .AddSwitch(OnUsePowerToggled, powerToggleBounds, "usePowerToggle", 24)

                .AddStaticText(UIUtils.I18n("Switchboard.Gui.PowerGauge"), CairoFont.WhiteSmallText(), powerGaugeLabelBounds)

                .AddInteractiveElement(powerGauge.Element, "powerGaugeBar")

                .AddDynamicText("", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), powerPercentBounds, "powerPercentText")

                .Compose();



            networkNameInput = SingleComposer.GetElement("networkNameInput") as GuiElementTextInput;


            powerPercentText = SingleComposer.GetDynamicText("powerPercentText");

            lastKnownServerCustomName = switchboard.GetNetworkCustomNameForEditor() ?? "";

            pendingNetworkName = lastKnownServerCustomName;

            networkNameInput?.SetValue(pendingNetworkName);

            var usePowerToggle = SingleComposer.GetSwitch("usePowerToggle");
            if (usePowerToggle != null)
            {
                usePowerToggle.On = switchboard.UsePowerRequirements;
            }



            RefreshData();

        }



        public void RefreshData()

        {

            if (SingleComposer == null)

            {

                return;

            }



            string customName = switchboard.GetNetworkCustomNameForEditor() ?? "";

            if (customName != lastKnownServerCustomName)

            {

                lastKnownServerCustomName = customName;

                pendingNetworkName = customName;

                networkNameInput?.SetValue(customName);

            }



            float ratio = Math.Max(0f, Math.Min(1f, switchboard.PowerPercent));

            powerGauge?.SetRatio01(ratio);

            int pct = (int)Math.Round(ratio * 100f);

            powerPercentText?.SetNewText($"{pct}%");

        }



        public override string ToggleKeyCombinationCode => null;



        private void OnTitleBarCloseClicked()

        {

            TryClose();

        }



        private void OnNetworkNameInputChanged(string value)

        {

            pendingNetworkName = value ?? "";

        }



        private bool OnSaveNetworkNameClicked()

        {

            string candidate = (pendingNetworkName ?? "").Trim();

            if (candidate.Length > 0 && WireNetworkHandler.IsNetworkNameTaken(switchboard.NetworkUID, candidate))

            {

                capi?.TriggerChatMessage(UIUtils.I18n("Switchboard.Settings.NameAlreadyUsed"));

                return true;

            }



            switchboard.RequestRenameNetwork(pendingNetworkName);

            return true;

        }

        private void OnUsePowerToggled(bool enabled)

        {

            switchboard.RequestSetUsePowerRequirements(enabled);

        }

    }

}

