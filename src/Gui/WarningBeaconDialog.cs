using System;
using System.IO;
using System.Reflection;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Inventory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.Gui
{
    public class WarningBeaconDialog : GuiDialogBlockEntityInventory
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly string _title;
        private readonly BlockPos _blockPos;
        private readonly BlockEntityLucerne _be;
        private readonly Action _onClose;
        private GuiElement _lightButton;
        private GuiElementStatbar _progressBar;
        private const int ProgressBarSteps = 100;

        public WarningBeaconDialog(string title, InventoryLucerne inventory, BlockPos blockPos, ICoreClientAPI capi, BlockEntityLucerne be, Action onClose)
            : base(title, inventory, blockPos, 2, capi)
        {
            _title = title;
            _blockPos = blockPos;
            _be = be;
            _onClose = onClose;
        }

        /// <summary>Send full inventory state so server stays in sync. Custom dialog cannot rely on game's slot packet format (InvNetworkUtil); full TreeAttribute sync avoids duplication.</summary>
        private void SendInvPacket(object packet)
        {
            byte[] data;
            using (var ms = new MemoryStream())
            {
                var tree = new TreeAttribute();
                Inventory.ToTreeAttributes(tree);
                tree.ToBytes(new BinaryWriter(ms));
                data = ms.ToArray();
            }
            capi.Network.SendBlockEntityPacket(_blockPos, BlockEntityLucerne.PacketIdInventory, data);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            ElementBounds slotGridBounds = ElementBounds.Fixed(0, 40, 160, 70);
            ElementBounds progressBounds = ElementBounds.Fixed(0, 112, 300, 25);
            ElementBounds buttonBounds = ElementBounds.Fixed(0, 147, 200, 35);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(slotGridBounds, progressBounds, buttonBounds);

            var progressBar = new GuiElementStatbar(capi, progressBounds, new double[3] { 0.1, 0.4, 0.1 }, false, false);
            progressBar.ShowValueOnHover = false;
            progressBar.SetValues(0, 0, ProgressBarSteps);

            SingleComposer = capi.Gui.CreateCompo("warningbeacon", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(_title, () => { TryClose(); })
                .AddItemSlotGrid(Inventory, SendInvPacket, 2, slotGridBounds, "slots")
                .AddInteractiveElement(progressBar, "progressBar")
                .AddDynamicText("", CairoFont.WhiteSmallText().WithFontSize(14).WithOrientation(EnumTextOrientation.Center), progressBounds.FlatCopy(), "hoursLabel")
                .AddButton(Lang.Get(RPVoiceChatMod.modID + ":WarningBeacon.LightButton") ?? "Allumer", () => { OnLightClicked(); return true; }, buttonBounds, EnumButtonStyle.Normal, "lightButton")
                .Compose();

            _progressBar = SingleComposer.GetStatbar("progressBar");
            _lightButton = SingleComposer.GetButton("lightButton");

            UpdateState();
        }

        private void OnLightClicked()
        {
            capi.Network.SendBlockEntityPacket(_blockPos, BlockEntityLucerne.PacketIdLightBeacon, Array.Empty<byte>());
            TryClose();
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
            UpdateState();
        }

        private void UpdateState()
        {
            var be = capi.World.BlockAccessor.GetBlockEntity(_blockPos) as BlockEntityLucerne;
            if (be == null || _lightButton == null) return;

            double remainingHours = be.BurnTimeRemainingGameHours;
            double fillRatio = be.IsBurning
                ? Math.Max(0, Math.Min(1, remainingHours / BlockEntityLucerne.BurnDurationGameHours))
                : 0;
            if (_progressBar != null)
                _progressBar.SetValue((int)(fillRatio * ProgressBarSteps));

            var hoursLabel = SingleComposer?.GetDynamicText("hoursLabel");
            if (hoursLabel != null)
            {
                if (be.IsBurning)
                    hoursLabel.SetNewText(Lang.Get(RPVoiceChatMod.modID + ":WarningBeacon.HoursRemaining", Math.Max(0, (int)Math.Ceiling(remainingHours))));
                else
                    hoursLabel.SetNewText("");
            }

            bool canLight = !be.IsBurning
                && ((InventoryLucerne)Inventory).FatSlot.StackSize >= BlockEntityLucerne.FatRequired
                && ((InventoryLucerne)Inventory).FirewoodSlot.StackSize >= BlockEntityLucerne.FirewoodRequired;
            SetButtonEnabled(_lightButton, canLight);
            var btnText = be.IsBurning ? Lang.Get(RPVoiceChatMod.modID + ":WarningBeacon.BurningButton") : Lang.Get(RPVoiceChatMod.modID + ":WarningBeacon.LightButton");
            SetButtonText(_lightButton, btnText);
        }

        private static void SetButtonEnabled(GuiElement button, bool enabled)
        {
            if (button == null) return;
            var prop = button.GetType().GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(button, enabled);
        }

        private static void SetButtonText(GuiElement button, string text)
        {
            if (button == null) return;
            var t = button.GetType();
            var setNewText = t.GetMethod("SetNewText", new[] { typeof(string) });
            if (setNewText != null)
                setNewText.Invoke(button, new object[] { text ?? "" });
            else
            {
                var setText = t.GetMethod("SetText", new[] { typeof(string) });
                setText?.Invoke(button, new object[] { text ?? "" });
            }
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            _be?.RefreshBonfireRenderer();
            _onClose?.Invoke();
        }
    }
}
