using System.Collections.Generic;
using System.Threading.Tasks;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityTelegraph : WireNode
    {
        TelegraphMenuDialog dialog;

        public bool IsPlaying { get; private set; }
        public int Volume { get; set; } = 8;
        private string sentMessage = "";
        private string receivedMessage = "";
        private Queue<char> pendingSignals = new Queue<char>();
        private const int MaxMessageLength = 100;

        public BlockEntityTelegraph() : base()
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
                dialog.UpdateSentText(sentMessage);
                dialog.UpdateReceivedText(receivedMessage);
            }
        }

        private void HandleReceivedSignal(object sender, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            OnReceivedSignal(message[0]);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            sentMessage = tree.GetString("sentMessage");
            receivedMessage = tree.GetString("receivedMessage");
            NetworkUID = tree.GetLong("rpvc:networkUID");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("sentMessage", sentMessage);
            tree.SetString("receivedMessage", receivedMessage);
            tree.SetLong("rpvc:networkUID", NetworkUID);
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

            if (sentMessage.Length >= MaxMessageLength)
                return; // On arrête si le message est trop long

            pendingSignals.Enqueue(keyChar);
            sentMessage += keyChar;
            MarkDirty();
            dialog?.UpdateSentText(sentMessage);

            clientApi.Network.GetChannel(WireNetworkHandler.NetworkChannel)
                .SendPacket(new WireNetworkMessage() { NetworkUID = NetworkUID, Message = $"{keyChar}", SenderPos = Pos });

            if (!IsPlaying)
            {
                _ = ProcessNextSignalAsync();
            }
        }

        private async Task ProcessNextSignalAsync()
        {
            while (pendingSignals.Count > 0)
            {
                char next = pendingSignals.Dequeue();
                await PlayMorseAsync(ConvertKeyCodeToMorse(next));
            }
        }

        public void OnReceivedSignal(char keyChar)
        {
            if (Api.Side != EnumAppSide.Client)
                return;

            if (Api is ICoreClientAPI clientApi)
            {
                receivedMessage += keyChar;
                MarkDirty();
                dialog?.UpdateReceivedText(receivedMessage);
            }

            // Lecture morse asynchrone
            Task.Run(() => PlayMorseAsync(ConvertKeyCodeToMorse(keyChar)));
        }

        private async Task PlayMorseAsync(string morse)
        {
            if (Api.Side == EnumAppSide.Server || IsPlaying)
                return;

            ICoreClientAPI capi = (ICoreClientAPI)Api;

            IsPlaying = true;

            //capi.SendChatMessage($"Transmitting: {morse}");

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

        public string GetSentMessage()
        {
            return sentMessage;
        }

        public string GetReceivedMessage()
        {
            return receivedMessage;
        }


        public void ClearMessages()
        {
            sentMessage = "";
            receivedMessage = "";
            MarkDirty();
            pendingSignals.Clear();
            dialog?.UpdateSentText("");
            dialog?.UpdateReceivedText("");
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

    /*public class TelegraphBlock : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Code.ToShortString() == "rpvoicechat:telegraphwire")
                return false; // Ne pas ouvrir le menu si joueur tient un câble télégraphe

            BlockEntityTelegraph telegraph = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityTelegraph;
            telegraph?.OnInteract();

            return true;
        }
    }*/
}