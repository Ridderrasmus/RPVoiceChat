using RPVoiceChat.Gui;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Systems;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.Blocks
{
    public class TelegraphBlockEntity : WireNode
    {
        TelegraphMenuDialog dialog;

        public bool IsPlaying { get; private set; }
        public int Volume { get; set; } = 8;

        public TelegraphBlockEntity() : base()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            IsPlaying = false;

            OnReceivedSignalEvent += HandleReceivedSignal;

            if (api.Side == EnumAppSide.Client)
            {
                dialog = new TelegraphMenuDialog((ICoreClientAPI)api, this);
            }
        }

        private void HandleReceivedSignal(object sender, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            OnReceivedSignal(message[0]);
        }

        public bool OnInteract()
        {
            if (Api.Side == EnumAppSide.Server)
                return true;

            dialog.TryOpen();
            return true;
        }

        public void SendSignal(char keyChar)
        {
            if (Api.Side != EnumAppSide.Client)
                return;

            if (Api is not ICoreClientAPI clientApi)
                return;

            clientApi.Network.GetChannel(WireNetworkHandler.NetworkChannel)
                .SendPacket(new WireNetworkMessage() { NetworkUID = NetworkUID, Message = $"{keyChar}", SenderPos = Pos });
        }

        public void OnReceivedSignal(char keyChar)
        {
            if (Api.Side != EnumAppSide.Client)
                return;

            Api.Logger.Debug($"Received signal: {keyChar}");

            // Lance la lecture morse asynchrone sans bloquer
            Task.Run(() => PlayMorseAsync(ConvertKeyCodeToMorse(keyChar)));
        }

        private async Task PlayMorseAsync(string morse)
        {
            if (Api.Side == EnumAppSide.Server || IsPlaying)
                return;

            ICoreClientAPI capi = (ICoreClientAPI)Api;

            /*var playerPos = capi.World.Player.Entity.Pos.XYZ;
            var blockPos = Pos.ToVec3d();

            double distance = playerPos.DistanceTo(blockPos);

            if (distance > 5.0) 
            {
                return;
            }*/

            IsPlaying = true;

            capi.SendChatMessage($"Transmitting: {morse}");

            foreach (char c in morse)
            {
                if (c == '.')
                    capi.Event.EnqueueMainThreadTask(() =>
                        capi.World.PlaySoundAt(new AssetLocation(RPVoiceChatMod.modID, "sounds/morse/dot"), Pos.X, Pos.Y, Pos.Z, randomizePitch: false, range: Volume),
                        "PlayMorse");
                else if (c == '-')
                    capi.Event.EnqueueMainThreadTask(() =>
                        capi.World.PlaySoundAt(new AssetLocation(RPVoiceChatMod.modID, "sounds/morse/dash"), Pos.X, Pos.Y, Pos.Z, randomizePitch: false, range: Volume),
                        "PlayMorse");

                await Task.Delay(500);
            }

            IsPlaying = false;
        }

        private static string ConvertKeyCodeToMorse(char keyChar)
        {
            switch (char.ToUpper(keyChar))
            {
                case 'A': return ".-";
                case 'B': return "-...";
                case 'C': return "-.-.";
                case 'D': return "-..";
                case 'E': return ".";
                case 'F': return "..-.";
                case 'G': return "--.";
                case 'H': return "....";
                case 'I': return "..";
                case 'J': return ".---";
                case 'K': return "-.-";
                case 'L': return ".-..";
                case 'M': return "--";
                case 'N': return "-.";
                case 'O': return "---";
                case 'P': return ".--.";
                case 'Q': return "--.-";
                case 'R': return ".-.";
                case 'S': return "...";
                case 'T': return "-";
                case 'U': return "..-";
                case 'V': return "...-";
                case 'W': return ".--";
                case 'X': return "-..-";
                case 'Y': return "-.--";
                case 'Z': return "--..";
                case '0': return "-----";
                case '1': return ".----";
                case '2': return "..---";
                case '3': return "...--";
                case '4': return "....-";
                case '5': return ".....";
                case '6': return "-....";
                case '7': return "--...";
                case '8': return "---..";
                case '9': return "----.";
                case '.': return ".-.-.-";
                default: return "";
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
