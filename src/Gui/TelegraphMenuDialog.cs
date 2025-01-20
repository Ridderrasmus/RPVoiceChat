using RPVoiceChat.Blocks;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RPVoiceChat.Gui
{
    public class TelegraphMenuDialog : GuiDialog
    {
        // Block that this menu is for
        private TelegraphBlockEntity telegraphBlock;
        

        public TelegraphMenuDialog(ICoreClientAPI capi, TelegraphBlockEntity telegraphBlock) : base(capi)
        {
            this.telegraphBlock = telegraphBlock;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            SingleComposer = capi.Gui.CreateCompo("telegraphmenu", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle));
        }

        public override void OnKeyPress(KeyEvent args)
        {
            if (args.KeyCode == (int)GlKeys.Escape)
                TryClose();

            if (telegraphBlock.IsPlaying)
                return;

            telegraphBlock.SendSignal(args.KeyChar);
        }

        public override bool CaptureAllInputs() => true;
        public override string ToggleKeyCombinationCode => null;
    }
}
