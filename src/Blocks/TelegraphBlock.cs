using RPVoiceChat.Gui;
using RPVoiceChat.Systems;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.Blocks
{
    public class TelegraphBlock : WireNode
    {
        TelegraphMenuDialog dialog;

        public bool IsPlaying { get; private set; }
        private BlockPos pos;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            IsPlaying = false;

            if (api.Side == EnumAppSide.Client)
                dialog = new TelegraphMenuDialog((ICoreClientAPI)api, this);
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            pos = blockPos;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api.Side == EnumAppSide.Server) 
                return true;
            
            dialog.TryOpen();
            return true;
        }

        public void SendSignal(int KeyCode)
        {
            PlayMorse(ConvertKeyCodeToMorse(KeyCode));
            if (api.Side == EnumAppSide.Server)
            {
                WireNetwork network = WireNetworkHandler.GetNetwork(NetworkUID);
                if (network != null)
                    network.SendSignal(this, $"{KeyCode}");
            }
        }

        public void OnRecievedSignal(int KeyCode)
        {
            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;

                PlayMorse(ConvertKeyCodeToMorse(KeyCode));
            }
        }

        private void PlayMorse(string morse)
        {
            if (api.Side == EnumAppSide.Server && !IsPlaying)
                return;

            ICoreClientAPI capi = (ICoreClientAPI)api;

            IsPlaying = true;

            foreach (char c in (string)morse)
            {
                if (c == '.')
                    capi.World.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/morse/dot"), pos.X, pos.Y, pos.Z, randomizePitch: false, range: 16);
                else if (c == '-')
                    capi.World.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/morse/dash"), pos.X, pos.Y, pos.Z, randomizePitch: false, range: 16);
            }

            IsPlaying = false;
        }

        private string ConvertKeyCodeToMorse(int KeyCode)
        {
            switch ((GlKeys)KeyCode)
            {
                case GlKeys.A:
                    return ".-";
                case GlKeys.B:
                    return "-...";
                case GlKeys.C:
                    return "-.-.";
                case GlKeys.D:
                    return "-..";
                case GlKeys.E:
                    return ".";
                case GlKeys.F:
                    return "..-.";
                case GlKeys.G:
                    return "--.";
                case GlKeys.H:
                    return "....";
                case GlKeys.I:
                    return "..";
                case GlKeys.J:
                    return ".---";
                case GlKeys.K:
                    return "-.-";
                case GlKeys.L:
                    return ".-..";
                case GlKeys.M:
                    return "--";
                case GlKeys.N:
                    return "-.";
                case GlKeys.O:
                    return "---";
                case GlKeys.P:
                    return ".--.";
                case GlKeys.Q:
                    return "--.-";
                case GlKeys.R:
                    return ".-.";
                case GlKeys.S:
                    return "...";
                case GlKeys.T:
                    return "-";
                case GlKeys.U:
                    return "..-";
                case GlKeys.V:
                    return "...-";
                case GlKeys.W:
                    return ".--";
                case GlKeys.X:
                    return "-..-";
                case GlKeys.Y:
                    return "-.--";
                case GlKeys.Z:
                    return "--..";
                case GlKeys.Number0:
                    return "-----";
                case GlKeys.Number1:
                    return ".----";
                case GlKeys.Number2:
                    return "..---";
                case GlKeys.Number3:
                    return "...--";
                case GlKeys.Number4:
                    return "....-";
                case GlKeys.Number5:
                    return ".....";
                case GlKeys.Number6:
                    return "-....";
                case GlKeys.Number7:
                    return "--...";
                case GlKeys.Number8:
                    return "---..";
                case GlKeys.Number9:
                    return "----.";
                case GlKeys.Keypad0:
                    return "-----";
                case GlKeys.Keypad1:
                    return ".----";
                case GlKeys.Keypad2:
                    return "..---";
                case GlKeys.Keypad3:
                    return "...--";
                case GlKeys.Keypad4:
                    return "....-";
                case GlKeys.Keypad5:
                    return ".....";
                case GlKeys.Keypad6:
                    return "-....";
                case GlKeys.Keypad7:
                    return "--...";
                case GlKeys.Keypad8:
                    return "---..";
                case GlKeys.Keypad9:
                    return "----.";
                case GlKeys.Period:
                    return ".-.-.-";
                default:
                    return "";
            }

        }
    }
}
