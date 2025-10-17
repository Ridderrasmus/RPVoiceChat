using System.Collections.Generic;
using System.Threading.Tasks;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityTelegraph : WireNode
    {
        TelegraphMenuDialog dialog;

        public bool IsPlaying { get; private set; }
        public int Volume { get; set; } = 8;
        public bool GenuineMorseCharacters => ServerConfigManager.TelegraphGenuineMorseCharacters;
        private int MaxMessageLength => ServerConfigManager.TelegraphMaxMessageLength;

        private string sentMessage = "";
        private string receivedMessage = "";
        private string sentMessageOriginal = ""; // Store original latin characters
        private string receivedMessageOriginal = ""; // Store original latin characters
        private Queue<char> pendingSignals = new Queue<char>();
        
        // Printer functionality
        private BlockEntityPrinter connectedPrinter;
        private long lastActivityTime;
        private int AutoSaveDelaySeconds => ServerConfigManager.PrinterAutoSaveDelaySeconds;

        public BlockEntityTelegraph() : base()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            IsPlaying = false;
            lastActivityTime = api.World.Calendar.TotalDays;

            OnReceivedSignalEvent += HandleReceivedSignal;

            if (api.Side == EnumAppSide.Client)
            {
                dialog = new TelegraphMenuDialog((ICoreClientAPI)api, this);
                UpdateDisplayMessages();
                dialog.UpdateSentText(sentMessage);
                dialog.UpdateReceivedText(receivedMessage);
            }
            
            // Check for printer below
            CheckForPrinterBelow();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            sentMessageOriginal = tree.GetString("sentMessage");
            receivedMessageOriginal = tree.GetString("receivedMessage");
            UpdateDisplayMessages();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("sentMessage", sentMessageOriginal);
            tree.SetString("receivedMessage", receivedMessageOriginal);
        }

        protected override void SetWireAttachmentOffset()
        {
            WireAttachmentOffset = new Vec3f(0.5f, 0.1f, 0.5f);
        }

        public bool OnInteract()
        {
            if (Api.Side == EnumAppSide.Server)
                return true;

            // Update display messages before opening dialog
            UpdateDisplayMessages();
            MarkDirty(); // Mark as dirty after updating display messages
            dialog?.UpdateSentText(sentMessage);
            dialog?.UpdateReceivedText(receivedMessage);
            
            dialog.TryOpen();
            return true;
        }


        private async Task ProcessNextSignalAsync()
        {
            while (pendingSignals.Count > 0)
            {
                char next = pendingSignals.Dequeue();
                await PlayMorseAsync(ConvertKeyCodeToMorse(next));
            }
        }

        public void SendSignal(char keyChar)
        {
            if (Api.Side != EnumAppSide.Client)
                return;

            if (Api is not ICoreClientAPI clientApi)
                return;

            if (sentMessage.Length >= MaxMessageLength)
                return; // Stop if message is too long

            UpdateActivityTime();
            pendingSignals.Enqueue(keyChar);

            string messageToSend = keyChar.ToString(); // Always send latin characters on network
            string displayPart = GenuineMorseCharacters ? ConvertKeyCodeToMorse(keyChar) : keyChar.ToString();

            sentMessageOriginal += keyChar.ToString(); // Store original latin character
            sentMessage += displayPart;
            MarkDirty();
            dialog?.UpdateSentText(sentMessage);

            if (!string.IsNullOrEmpty(messageToSend))
            {
                clientApi.Network.GetChannel(WireNetworkHandler.NetworkChannel)
                    .SendPacket(new WireNetworkMessage()
                    {
                        NetworkUID = NetworkUID,
                        Message = messageToSend,
                        SenderPos = Pos
                    });
            }

            if (!IsPlaying)
            {
                _ = ProcessNextSignalAsync();
            }
        }

        public void OnReceivedSignal(char keyChar)
        {
            if (Api.Side != EnumAppSide.Client)
                return;

            UpdateActivityTime();

            if (Api is ICoreClientAPI clientApi)
            {
                string displayChar = GenuineMorseCharacters ? ConvertKeyCodeToMorse(keyChar) : keyChar.ToString();
                
                receivedMessageOriginal += keyChar.ToString(); // Store original latin character
                receivedMessage += displayChar;

                MarkDirty();
                dialog?.UpdateReceivedText(receivedMessage);
            }

            Task.Run(() => PlayMorseAsync(ConvertKeyCodeToMorse(keyChar)));
        }


        private void HandleReceivedSignal(object sender, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            // Always receive latin characters on network, convert to morse for display if needed
            char keyChar = message[0];
            OnReceivedSignal(keyChar);
        }

        private async Task PlayMorseAsync(string morse)
        {
            if (Api.Side == EnumAppSide.Server || IsPlaying)
                return;

            ICoreClientAPI capi = (ICoreClientAPI)Api;

            IsPlaying = true;

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
            sentMessageOriginal = "";
            receivedMessageOriginal = "";
            MarkDirty();
            pendingSignals.Clear();
            dialog?.UpdateSentText("");
            dialog?.UpdateReceivedText("");
        }

        private void UpdateDisplayMessages()
        {
            // Convert original messages to display format based on current mode
            sentMessage = "";
            receivedMessage = "";
            
            foreach (char c in sentMessageOriginal)
            {
                sentMessage += GenuineMorseCharacters ? ConvertKeyCodeToMorse(c) : c.ToString();
            }
            
            foreach (char c in receivedMessageOriginal)
            {
                receivedMessage += GenuineMorseCharacters ? ConvertKeyCodeToMorse(c) : c.ToString();
            }
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

        // Printer functionality methods
        private void CheckForPrinterBelow()
        {
            if (Api?.World?.BlockAccessor == null) return;

            BlockPos belowPos = Pos.DownCopy();
            BlockEntity belowEntity = Api.World.BlockAccessor.GetBlockEntity(belowPos);
            
            if (belowEntity is BlockEntityPrinter printer)
            {
                ConnectToPrinter(printer);
            }
        }

        private void ConnectToPrinter(BlockEntityPrinter printer)
        {
            connectedPrinter = printer;
            
            // Register with printer system for auto-save functionality
            if (Api.Side == EnumAppSide.Server)
            {
                var printerSystem = Api.ModLoader.GetModSystem<PrinterSystem>();
                printerSystem?.RegisterTelegraphWithPrinter(this);
            }
        }

        public void SetPrinter(BlockEntityPrinter printer)
        {
            connectedPrinter = printer;
        }

        private void UpdateActivityTime()
        {
            lastActivityTime = Api.World.Calendar.TotalDays;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            CheckForPrinterBelow();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            DisconnectFromPrinter();
        }

        private void DisconnectFromPrinter()
        {
            if (connectedPrinter != null)
            {
                // Unregister from printer system
                if (Api.Side == EnumAppSide.Server)
                {
                    var printerSystem = Api.ModLoader.GetModSystem<PrinterSystem>();
                    printerSystem?.UnregisterTelegraphWithPrinter(this);
                }
                
                connectedPrinter = null;
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            DisconnectFromPrinter();
        }


        // Auto-save functionality
        public void CheckAutoSave()
        {
            if (connectedPrinter == null) return;

            long currentTime = Api.World.Calendar.TotalDays;
            long timeSinceLastActivity = currentTime - lastActivityTime;

            // Convert days to seconds (assuming 24 hours per day)
            double secondsSinceLastActivity = timeSinceLastActivity * 24 * 60 * 60;

            if (secondsSinceLastActivity >= AutoSaveDelaySeconds && !string.IsNullOrEmpty(receivedMessageOriginal))
            {
                // Auto-save the received message
                connectedPrinter.CreateTelegram(receivedMessageOriginal);
                
                // Clear the message after saving
                receivedMessage = "";
                receivedMessageOriginal = "";
                MarkDirty();
                dialog?.UpdateReceivedText("");
            }
        }

    }
}