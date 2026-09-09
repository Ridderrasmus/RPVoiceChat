using RPVoiceChat.GameContent.Items;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    public class RadioTalkieDialog : GuiDialog
    {
        private const string VolumeSliderKey = "radioTalkieListenVolumeSlider";
        private const int SettingsSyncDebounceMs = 250;

        private static RadioTalkieDialog currentDialog;

        private ItemSlot talkieSlot;
        private GuiElementTextInput frequencyInput;
        private NamedSlider listenVolumeSlider;
        private string pendingFrequency = "";
        private long pendingSettingsSyncId = 0;

        public RadioTalkieDialog(ICoreClientAPI capi, ItemSlot talkieSlot) : base(capi)
        {
            BindTalkieSlot(talkieSlot);
            currentDialog = this;
        }

        public static void CloseIfOpen()
        {
            if (currentDialog != null && currentDialog.IsOpened())
            {
                currentDialog.TryClose();
            }
        }

        public static void OpenOrFocus(ICoreClientAPI capi, ItemSlot talkieSlot)
        {
            if (capi?.World?.Player?.Entity?.Controls?.Sneak != true)
            {
                return;
            }

            if (currentDialog == null)
            {
                currentDialog = new RadioTalkieDialog(capi, talkieSlot);
            }
            else
            {
                currentDialog.BindTalkieSlot(talkieSlot);
            }

            if (!currentDialog.IsOpened())
            {
                currentDialog.TryOpen();
            }
        }

        public void BindTalkieSlot(ItemSlot slot)
        {
            talkieSlot = slot;
            if (talkieSlot?.Itemstack != null)
            {
                pendingFrequency = ItemRadio.GetTunedFrequency(talkieSlot.Itemstack);
            }
        }

        public override string ToggleKeyCombinationCode => null;

        public override void OnGuiOpened()
        {
            // GuiDialog (non block-entity): do not call base.OnGuiOpened — it composes a dialog
            // that is immediately replaced here and leaks Cairo surfaces.
            if (talkieSlot?.Itemstack != null)
            {
                pendingFrequency = ItemRadio.GetTunedFrequency(talkieSlot.Itemstack);
            }

            BuildComposer();
        }

        public override void OnGuiClosed()
        {
            CancelPendingSettingsSync();
            ReleaseComposer();
            if (currentDialog == this)
            {
                currentDialog = null;
            }

            base.OnGuiClosed();
        }

        public override void Dispose()
        {
            CancelPendingSettingsSync();
            ReleaseComposer();
            if (currentDialog == this)
            {
                currentDialog = null;
            }

            base.Dispose();
        }

        private void ReleaseComposer()
        {
            SingleComposer?.Dispose();
            frequencyInput = null;
            listenVolumeSlider = null;
        }

        private void BuildComposer()
        {
            ReleaseComposer();

            int listenVolume = ItemRadio.GetListenVolumePercent(talkieSlot?.Itemstack);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds labelBounds = ElementBounds.Fixed(0, 35, 320, 18);
            ElementBounds inputBounds = ElementBounds.Fixed(0, 55, 220, 26);
            ElementBounds saveBounds = ElementBounds.Fixed(232, 55, 88, 26);
            ElementBounds inventoryListenLabelBounds = ElementBounds.Fixed(0, 88, 280, 18);
            ElementBounds inventoryListenToggleBounds = ElementBounds.Fixed(288, 84, 32, 24);
            ElementBounds volumeLabelBounds = ElementBounds.Fixed(0, 120, 320, 18);
            ElementBounds volumeSliderBounds = ElementBounds.Fixed(0, 140, 320, 26);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(
                labelBounds,
                inputBounds,
                saveBounds,
                inventoryListenLabelBounds,
                inventoryListenToggleBounds,
                volumeLabelBounds,
                volumeSliderBounds);

            listenVolumeSlider = new NamedSlider(capi, VolumeSliderKey, OnListenVolumeChanged, volumeSliderBounds);
            listenVolumeSlider.SetValues(
                listenVolume,
                ItemRadio.MinListenVolumePercent,
                ItemRadio.MaxListenVolumePercent,
                1,
                "%");

            SingleComposer = capi.Gui.CreateCompo("radiotalkie", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Radio.Talkie.Gui.Title"), OnTitleBarCloseClicked)
                .AddStaticText(UIUtils.I18n("Radio.Talkie.Gui.Frequency"), CairoFont.WhiteSmallText(), labelBounds)
                .AddTextInput(inputBounds, value => pendingFrequency = value ?? "", CairoFont.TextInput(), "radioTalkieFrequencyInput")
                .AddSmallButton(UIUtils.I18n("Radio.Gui.Save"), OnSaveFrequencyClicked, saveBounds)
                .AddStaticText(UIUtils.I18n("Radio.Talkie.Gui.InventoryListen"), CairoFont.WhiteSmallText(), inventoryListenLabelBounds)
                .AddSwitch(OnInventoryListenToggled, inventoryListenToggleBounds, "radioTalkieInventoryListen", 24)
                .AddStaticText(UIUtils.I18n("Radio.Talkie.Gui.ListenVolume"), CairoFont.WhiteSmallText(), volumeLabelBounds)
                .AddInteractiveElement(listenVolumeSlider, VolumeSliderKey)
                .Compose();

            frequencyInput = SingleComposer.GetTextInput("radioTalkieFrequencyInput");
            frequencyInput?.SetValue(pendingFrequency);

            var inventoryListenToggle = SingleComposer.GetSwitch("radioTalkieInventoryListen");
            if (inventoryListenToggle != null)
            {
                inventoryListenToggle.On = ItemRadio.GetInventoryListen(talkieSlot?.Itemstack);
            }
        }

        private void OnInventoryListenToggled(bool on)
        {
            if (talkieSlot?.Itemstack == null)
            {
                return;
            }

            ItemRadio.SetInventoryListen(talkieSlot.Itemstack, on);
            ItemRadio.SendTalkieSettings(capi, talkieSlot);
        }

        private bool OnListenVolumeChanged(int value, string _)
        {
            if (talkieSlot?.Itemstack == null)
            {
                return true;
            }

            ItemRadio.SetListenVolumePercent(talkieSlot.Itemstack, value);
            QueueSettingsSync();
            return true;
        }

        private bool OnSaveFrequencyClicked()
        {
            if (talkieSlot?.Itemstack != null)
            {
                ItemRadio.SetTunedFrequency(talkieSlot.Itemstack, pendingFrequency);
                talkieSlot.MarkDirty();
                ItemRadio.SendTalkieSettings(capi, talkieSlot);
                capi.World.Player.InventoryManager.BroadcastHotbarSlot();
            }

            return true;
        }

        private void QueueSettingsSync()
        {
            CancelPendingSettingsSync();
            pendingSettingsSyncId = capi.Event.RegisterCallback(_ =>
            {
                pendingSettingsSyncId = 0;
                if (talkieSlot?.Itemstack != null)
                {
                    ItemRadio.SendTalkieSettings(capi, talkieSlot);
                }
            }, SettingsSyncDebounceMs);
        }

        private void CancelPendingSettingsSync()
        {
            if (pendingSettingsSyncId != 0)
            {
                capi.Event.UnregisterCallback(pendingSettingsSyncId);
                pendingSettingsSyncId = 0;
            }
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
}
