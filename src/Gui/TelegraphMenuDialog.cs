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
        private TelegraphBlock telegraphBlock;
        
        // List of keys that can be pressed
        private static readonly GlKeys[] keys = new GlKeys[] 
        {
            GlKeys.A, GlKeys.B, GlKeys.C, GlKeys.D, GlKeys.E, GlKeys.F, GlKeys.G, GlKeys.H, GlKeys.I, GlKeys.J, GlKeys.K, 
            GlKeys.L, GlKeys.M, GlKeys.N, GlKeys.O, GlKeys.P, GlKeys.Q, GlKeys.R, GlKeys.S, GlKeys.T, GlKeys.U, GlKeys.V, 
            GlKeys.W, GlKeys.X, GlKeys.Y, GlKeys.Z
        };

        public TelegraphMenuDialog(ICoreClientAPI capi, TelegraphBlock telegraphBlock) : base(capi)
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

            telegraphBlock.SendSignal(args.KeyCode);
        }

        public override bool CaptureAllInputs() => true;
        public override string ToggleKeyCombinationCode => null;
    }
}
