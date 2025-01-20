using RPVoiceChat.Gui;
using RPVoiceChat.src.Systems;
using RPVoiceChat.Systems;
using System.Linq;
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

            OnRecievedSignalEvent += (sender, message) => OnRecievedSignal(message[0]);

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

        public void SendSignal(char KeyChar)
        {
            if (Api.Side == EnumAppSide.Server)
                return;

            (Api as ICoreClientAPI).Network.GetChannel(WireNetworkHandler.NetworkChannel).SendPacket(new WireNetworkMessage() { NetworkUID = NetworkUID, Message = $"{KeyChar}", SenderPos = Pos });

            return;

            WireNetwork network = WireNetworkHandler.GetNetwork(NetworkUID);

            if (network != null)
            {

                network.SendSignal(this, $"{KeyChar}");
            }
        }

        public void OnRecievedSignal(char KeyChar)
        {
            Api.Logger.Debug($"Recieved signal: {KeyChar}");

            Task.Run(() => PlayMorseAsync(ConvertKeyCodeToMorse(KeyChar)));
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

        private string ConvertKeyCodeToMorse(char KeyChar)
        {
            Api.Logger.Debug($"Converting {KeyChar} to morse");
            Api.Logger.Debug($"Key: {KeyChar}");
            switch (char.ToUpper(KeyChar))
            {
                case 'A':
                    return ".-";
                case 'B':
                    return "-...";
                case 'C':
                    return "-.-.";
                case 'D':
                    return "-..";
                case 'E':
                    return ".";
                case 'F':
                    return "..-.";
                case 'G':
                    return "--.";
                case 'H':
                    return "....";
                case 'I':
                    return "..";
                case 'J':
                    return ".---";
                case 'K':
                    return "-.-";
                case 'L':
                    return ".-..";
                case 'M':
                    return "--";
                case 'N':
                    return "-.";
                case 'O':
                    return "---";
                case 'P':
                    return ".--.";
                case 'Q':
                    return "--.-";
                case 'R':
                    return ".-.";
                case 'S':
                    return "...";
                case 'T':
                    return "-";
                case 'U':
                    return "..-";
                case 'V':
                    return "...-";
                case 'W':
                    return ".--";
                case 'X':
                    return "-..-";
                case 'Y':
                    return "-.--";
                case 'Z':
                    return "--..";
                case '0':
                    return "-----";
                case '1':
                    return ".----";
                case '2':
                    return "..---";
                case '3':
                    return "...--";
                case '4':
                    return "....-";
                case '5':
                    return ".....";
                case '6':
                    return "-....";
                case '7':
                    return "--...";
                case '8':
                    return "---..";
                case '9':
                    return "----.";
                case '.':
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
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Code.ToShortString() == "rpvoicechat:telegraphwire")
                return false; // Don't open the menu if the player is holding a telegraph wire

            TelegraphBlockEntity telegraph = world.BlockAccessor.GetBlockEntity(blockSel.Position) as TelegraphBlockEntity;
            telegraph?.OnInteract();

            return true;
        }
    }
}
