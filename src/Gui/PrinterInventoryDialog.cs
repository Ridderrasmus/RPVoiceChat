using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Gui
{
    public class PrinterInventoryDialog : GuiDialogBlockEntityInventory
    {
        private Action onCloseCallback;

        public PrinterInventoryDialog(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, int cols, ICoreClientAPI capi, Action onCloseCallback)
            : base(dialogTitle, inventory, blockEntityPos, cols, capi)
        {
            this.onCloseCallback = onCloseCallback;
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            onCloseCallback?.Invoke();
        }
    }
}
