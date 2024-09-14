﻿using RPVoiceChat.Gui;
using RPVoiceChat.src.Systems;
using RPVoiceChat.Systems;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.Blocks
{
    public class TelegraphBlockEntity : WireNode
    {
        TelegraphMenuDialog dialog;

        public bool IsPlaying { get; private set; }
        public int Volume { get; set; } = 8;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            IsPlaying = false;
            
            OnRecievedSignalEvent += (sender, message) => OnRecievedSignal(int.Parse(message));

            if (api.Side == EnumAppSide.Client)
            {
                dialog = new TelegraphMenuDialog((ICoreClientAPI)api, this);

            }
        }



        public bool OnInteract()
        {
            if (Api.Side == EnumAppSide.Server)
                return true;

            dialog.TryOpen();
            return true;
        }

        public void SendSignal(int KeyCode)
        {
            if (Api.Side == EnumAppSide.Server)
                return;

            (Api as ICoreClientAPI).Network.GetChannel(WireNetworkHandler.NetworkChannel).SendPacket(new WireNetworkMessage() { NetworkUID = NetworkUID, Message = $"{KeyCode}", SenderPos = Pos });

            return;

            WireNetwork network = WireNetworkHandler.GetNetwork(NetworkUID);

            if (network != null)
            {

                network.SendSignal(this, $"{KeyCode}");
            }
        }

        public void OnRecievedSignal(int KeyCode)
        {
            Api.Logger.Debug($"Recieved signal: {KeyCode}");

            Task.Run(() => PlayMorseAsync(ConvertKeyCodeToMorse(KeyCode)));
        }

        private async Task PlayMorseAsync(string morse)
        {
            if (Api.Side == EnumAppSide.Server || IsPlaying)
                return;

            ICoreClientAPI capi = (ICoreClientAPI)Api;

            IsPlaying = true;

            capi.SendChatMessage($"Transmitting: {morse}");

            foreach (char c in (string)morse)
            {
                if (c == '.')
                    capi.Event.EnqueueMainThreadTask(async () => capi.World.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/morse/dot"), Pos.X, Pos.Y, Pos.Z, randomizePitch: false, range: Volume), "PlayMorse");
                else if (c == '-')
                    capi.Event.EnqueueMainThreadTask(async () => capi.World.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/morse/dash"), Pos.X, Pos.Y, Pos.Z, randomizePitch: false, range: Volume), "PlayMorse");

                await Task.Delay(500);
            }

            IsPlaying = false;
        }

        private string ConvertKeyCodeToMorse(int KeyCode)
        {
            Api.Logger.Debug($"Converting {KeyCode} to morse");
            Api.Logger.Debug($"Key: {(GlKeys)KeyCode}");
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

    public class TelegraphBlock : Block 
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            TelegraphBlockEntity telegraph = world.BlockAccessor.GetBlockEntity(blockSel.Position) as TelegraphBlockEntity;
            telegraph?.OnInteract();

            return true;
        }
    }
}
