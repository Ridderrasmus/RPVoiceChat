using System;
using System.IO;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Inventory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Gui
{
    /// <summary>
    /// Same pattern as WarningBeaconDialog: explicit AddItemSlotGrid(..., "slots") + full inventory
    /// sync via BlockEntityPrinter.PacketIdPrinterInventory so ghost overlay can align on slot bounds.
    /// </summary>
    public class PrinterInventoryDialog : GuiDialogBlockEntityInventory
    {
        private readonly Action _onCloseCallback;
        private readonly BlockPos _blockPos;
        private readonly string _title;
        private ItemStack _ghostPaper;
        private ItemStack _ghostTelegram;

        public PrinterInventoryDialog(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, int cols, ICoreClientAPI capi, Action onCloseCallback)
            : base(dialogTitle, inventory, blockEntityPos, cols, capi)
        {
            _onCloseCallback = onCloseCallback;
            _blockPos = blockEntityPos;
            _title = dialogTitle;
        }

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
            capi.Network.SendBlockEntityPacket(_blockPos, BlockEntityPrinter.PacketIdPrinterInventory, data);
        }

        public override void OnGuiOpened()
        {
            // Same approach as WarningBeaconDialog: compose with named "slots" grid (no base.OnGuiOpened — avoids vanilla grid without name).
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            // 5×2 slots, vanilla slot size ~48
            const int slotSize = 48;
            int cols = 5;
            int rows = 2;
            ElementBounds slotGridBounds = ElementBounds.Fixed(0, 40, cols * slotSize, rows * slotSize);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(slotGridBounds);

            SingleComposer = capi.Gui.CreateCompo("printerinv", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(_title, () => { TryClose(); })
                .AddItemSlotGrid(Inventory, SendInvPacket, cols, slotGridBounds, "slots")
                .Compose();

            var world = capi.World;
            var paper = world.GetItem(new AssetLocation(RPVoiceChatMod.modID, "paperslip"));
            var telegram = world.GetItem(new AssetLocation(RPVoiceChatMod.modID, "telegram"));
            if (paper != null) _ghostPaper = new ItemStack(paper, 1);
            if (telegram != null) _ghostTelegram = new ItemStack(telegram, 1);
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
            DrawSlotGhosts();
        }

        private void DrawSlotGhosts()
        {
            if (Inventory is not InventoryPrinter inv || SingleComposer == null) return;
            var slotEl = SingleComposer.GetElement("slots");
            if (slotEl?.Bounds == null) return;

            double gx = slotEl.Bounds.renderX;
            double gy = slotEl.Bounds.renderY;
            const int cols = 5;
            const double rowPitch = 48;
            const double slotSize = 48;

            // Pas fixe 48 en X/Y comme le compose (5×48 × 2×48). Décalage cumulatif = notre origine/pas
            // ne colle pas au moteur ; correction linéaire par colonne (plus à droite chaque col).
            // Réglage empirique — Alt+F10 mode 2 pour voir le vert de la grille (wiki Modding:GUIs).
            const float telegramNudgeY = 13f;  // +3 px vers le bas
            const float telegramBaseX = 13f; // 1 px vers la gauche
            const float telegramPerColX = 3f; // +3 px par colonne → compense dérive à gauche

            void DrawSlot(ItemSlot slot, ItemStack ghost, int slotIndex, float ox = 0f, float oy = 0f)
            {
                if (ghost == null) return;
                int col = slotIndex % cols;
                int row = slotIndex / cols;
                double rectX = gx + col * slotSize;
                double rectY = gy + row * rowPitch;
                float oxTotal = ox + col * telegramPerColX;
                AllowedItemGhostDraw.DrawInRect(capi, slot, ghost, rectX, rectY, slotSize, slotSize,
                    offsetX: oxTotal, offsetY: oy);
            }

            // Même décalage que les télégrammes, puis 1 px à gauche pour le papier.
            DrawSlot(inv.PaperSlot, _ghostPaper, 0, telegramBaseX - 1f, telegramNudgeY);
            for (int i = 0; i < inv.TelegramSlots.Length; i++)
                DrawSlot(inv.TelegramSlots[i], _ghostTelegram, i + 1, telegramBaseX, telegramNudgeY);
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            _onCloseCallback?.Invoke();
        }
    }
}
