using RPVoiceChat.GameContent.Blocks;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Gui
{
    public class TelegraphMenuDialog : GuiDialog
    {
        private TelegraphBlockEntity telegraphBlock;

        // Pour anti-spam : temps du dernier envoi
        private long lastKeySentMs = 0;
        private const int MinDelayBetweenKeysMs = 200; // 200 ms entre deux touches max

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
            // Gestion de la fermeture avec Escape
            if (args.KeyCode == (int)GlKeys.Escape)
            {
                TryClose();
                return;
            }

            // Pas d’envoi si on est en train de jouer un son
            if (telegraphBlock.IsPlaying)
            {
                return;
            }

            // Anti-spam simple : on ne traite pas les touches trop rapprochées
            long nowMs = capi.World.ElapsedMilliseconds;
            if (nowMs - lastKeySentMs < MinDelayBetweenKeysMs) return;

            // Envoi du signal avec le caractère tapé
            if (args.KeyChar != '\0')
            {
                telegraphBlock.SendSignal(args.KeyChar);
                lastKeySentMs = nowMs;
            }
        }

        public override bool CaptureAllInputs() => true;
        public override string ToggleKeyCombinationCode => null;
    }
}
