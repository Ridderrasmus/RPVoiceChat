using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Systems;
using RPVoiceChat.Networking.Packets;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
        private int MessageDeletionDelaySeconds => ServerConfigManager.TelegraphMessageDeletionDelaySeconds;
        private bool isCountdownActive = false;
        private int countdownSeconds = 0;
        private long countdownEndTime = 0; // Time when countdown reached 0

        public BlockEntityTelegraph() : base()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            IsPlaying = false;
            lastActivityTime = api.World.ElapsedMilliseconds;

            OnReceivedSignalEvent += HandleReceivedSignal;

            if (api.Side == EnumAppSide.Client)
            {
                dialog = new TelegraphMenuDialog((ICoreClientAPI)api, this);
                UpdateDisplayMessages();
                dialog.UpdateSentText(sentMessage);
                dialog.UpdateReceivedText(receivedMessage);
                
                // Register client-side countdown update timer
                api.Event.RegisterGameTickListener(OnClientGameTick, 1000); // Every second
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
            
            // Update dialog if it exists
            if (Api?.Side == EnumAppSide.Client)
            {
                dialog?.UpdateSentText(sentMessage);
                dialog?.UpdateReceivedText(receivedMessage);
            }
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

        public void ProcessPrintPacket(string message)
        {
            // Trigger message printing/deletion
            if (connectedPrinter != null)
            {
                connectedPrinter.CreateTelegram(message, NetworkUID.ToString());
            }
            
            // Clear the message
            receivedMessage = "";
            receivedMessageOriginal = "";
            isCountdownActive = false;
            countdownSeconds = 0;
            countdownEndTime = 0;
            MarkDirty();
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
            Vintagestory.API.Common.BlockEntity belowEntity = Api.World.BlockAccessor.GetBlockEntity(belowPos);
            
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
            lastActivityTime = Api.World.ElapsedMilliseconds;
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


        // Client-side countdown update
        private void OnClientGameTick(float dt)
        {
            if (Api.Side != EnumAppSide.Client) return;
            
            long currentTime = Api.World.ElapsedMilliseconds;
            long timeSinceLastActivity = currentTime - lastActivityTime;
            double secondsSinceLastActivity = timeSinceLastActivity / 1000.0;

            // Start countdown if message is complete and countdown not already active
            if (!string.IsNullOrEmpty(receivedMessageOriginal) && !isCountdownActive && secondsSinceLastActivity >= 2.0)
            {
                StartCountdown();
            }

            // Update countdown if active
            if (isCountdownActive)
            {
                UpdateCountdown();
            }
        }

        // Auto-save functionality
        public void CheckAutoSave()
        {
            long currentTime = Api.World.ElapsedMilliseconds;
            long timeSinceLastActivity = currentTime - lastActivityTime;

            // Convert milliseconds to seconds
            double secondsSinceLastActivity = timeSinceLastActivity / 1000.0;

            // Start countdown if message is complete and countdown not already active
            if (!string.IsNullOrEmpty(receivedMessageOriginal) && !isCountdownActive && secondsSinceLastActivity >= 2.0)
            {
                StartCountdown();
            }

            // Update countdown if active
            if (isCountdownActive)
            {
                UpdateCountdown();
            }

            if (secondsSinceLastActivity >= MessageDeletionDelaySeconds + 2 && !string.IsNullOrEmpty(receivedMessageOriginal))
            {
                // If printer is connected, save the message before clearing
                if (connectedPrinter != null)
                {
                    connectedPrinter.CreateTelegram(receivedMessageOriginal, NetworkUID.ToString());
                }
                
                // Always clear the message after the delay (with or without printer)
                receivedMessage = "";
                receivedMessageOriginal = "";
                isCountdownActive = false;
                countdownSeconds = 0;
                countdownEndTime = 0;
                MarkDirty();
                dialog?.UpdateReceivedText("");
                dialog?.UpdateCountdown(-1); // Hide countdown completely
            }
        }

        private void StartCountdown()
        {
            isCountdownActive = true;
            countdownSeconds = MessageDeletionDelaySeconds; // Start from the delay (e.g., 10 seconds)
            dialog?.UpdateCountdown(countdownSeconds);
        }

        private void UpdateCountdown()
        {
            long currentTime = Api.World.ElapsedMilliseconds;
            long timeSinceLastActivity = currentTime - lastActivityTime;
            double secondsSinceLastActivity = timeSinceLastActivity / 1000.0;
            
            // Calculate countdown: starts 2 seconds after last activity, counts down for 10 seconds
            // So at 2 seconds: countdown = 10, at 12 seconds: countdown = 0
            int newCountdown = Math.Max(0, MessageDeletionDelaySeconds - (int)secondsSinceLastActivity + 2);
            
            if (newCountdown != countdownSeconds)
            {
                countdownSeconds = newCountdown;
                dialog?.UpdateCountdown(countdownSeconds);
                
                // If countdown just reached 0, record the time
                if (countdownSeconds == 0 && countdownEndTime == 0)
                {
                    countdownEndTime = currentTime;
                    
                    // Send packet to server to trigger printing
                    if (Api.Side == EnumAppSide.Client)
                    {
                        var packet = new TelegraphPrintPacket
                        {
                            Message = receivedMessageOriginal,
                            TelegraphPos = Pos
                        };
                        RPVoiceChatMod.TelegraphPrintClientChannel.SendPacket(packet);
                    }
                }
            }
            
            // Hide countdown 2 seconds after it reached 0
            if (countdownSeconds == 0 && countdownEndTime > 0)
            {
                double timeSinceCountdownEnd = (currentTime - countdownEndTime) / 1000.0;
                if (timeSinceCountdownEnd >= 2.0)
                {
                    dialog?.UpdateCountdown(-1); // Hide countdown
                    isCountdownActive = false;
                    countdownEndTime = 0;
                }
            }
        }

    }
}