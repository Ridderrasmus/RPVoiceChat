using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Systems;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Client.Tesselation;

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
        private long lastReceivedActivityTime;
        private long lastSentActivityTime;
        private int MessageDeletionDelaySeconds => ServerConfigManager.TelegraphMessageDeletionDelaySeconds;
        private bool isReceivedCountdownActive = false;
        private int receivedCountdownSeconds = 0;
        private long receivedCountdownEndTime = 0;
        private bool isSentCountdownActive = false;
        private int sentCountdownSeconds = 0;
        private long sentCountdownEndTime = 0;

        // Animation util pour jouer l'animation "click" et gérer l'orientation
        public BlockEntityAnimationUtil animUtil { get { return this.GetAnimUtil(); } }

        public BlockEntityTelegraph() : base()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            IsPlaying = false;
            lastReceivedActivityTime = api.World.ElapsedMilliseconds;
            lastSentActivityTime = api.World.ElapsedMilliseconds;

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
                
            // Check if NetworkUID is valid
            if (NetworkUID == 0)
            {
                Api.Logger.Warning("Cannot send signal: NetworkUID is 0 (not connected to network)");
                return;
            }

            UpdateActivityTime();
            UpdateSentActivityTime(); // Update sent message activity time
            pendingSignals.Enqueue(keyChar);
            
            string messageToSend = keyChar.ToString(); // Always send latin characters on network
            string displayPart = GenuineMorseCharacters ? ConvertKeyCodeToMorse(keyChar) : keyChar.ToString();

            sentMessageOriginal += keyChar.ToString(); // Store original latin character
            sentMessage += displayPart;
            MarkDirty();
            dialog?.UpdateSentText(sentMessage);
            
            TriggerKeyClickAnimation();

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
            isReceivedCountdownActive = false;
            receivedCountdownSeconds = 0;
            receivedCountdownEndTime = 0;
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
            
            // Reset countdowns
            isReceivedCountdownActive = false;
            receivedCountdownSeconds = 0;
            receivedCountdownEndTime = 0;
            isSentCountdownActive = false;
            sentCountdownSeconds = 0;
            sentCountdownEndTime = 0;
            dialog?.UpdateCountdown(-1);
            dialog?.UpdateSentCountdown(-1);
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
            lastReceivedActivityTime = Api.World.ElapsedMilliseconds;
        }

        private void UpdateSentActivityTime()
        {
            lastSentActivityTime = Api.World.ElapsedMilliseconds;
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
            
            // Handle received message countdown
            long timeSinceLastReceivedActivity = currentTime - lastReceivedActivityTime;
            double secondsSinceLastReceivedActivity = timeSinceLastReceivedActivity / 1000.0;

            // Start countdown if received message is complete and countdown not already active
            if (!string.IsNullOrEmpty(receivedMessageOriginal) && !isReceivedCountdownActive && secondsSinceLastReceivedActivity >= 2.0)
            {
                StartReceivedCountdown();
            }

            // Update received message countdown if active
            if (isReceivedCountdownActive)
            {
                UpdateReceivedCountdown();
            }

            // Handle sent message countdown
            long timeSinceLastSentActivity = currentTime - lastSentActivityTime;
            double secondsSinceLastSentActivity = timeSinceLastSentActivity / 1000.0;

            // Start countdown if sent message is complete and countdown not already active
            // Only start countdown if user has stopped typing for a reasonable time
            if (!string.IsNullOrEmpty(sentMessageOriginal) && !isSentCountdownActive && secondsSinceLastSentActivity >= 3.0)
            {
                StartSentCountdown();
            }

            // Update sent message countdown if active
            if (isSentCountdownActive)
            {
                UpdateSentCountdown();
            }
        }

        // Auto-save functionality
        public void CheckAutoSave()
        {
            long currentTime = Api.World.ElapsedMilliseconds;
            long timeSinceLastReceivedActivity = currentTime - lastReceivedActivityTime;

            // Convert milliseconds to seconds
            double secondsSinceLastReceivedActivity = timeSinceLastReceivedActivity / 1000.0;

            // Start countdown if received message is complete and countdown not already active
            if (!string.IsNullOrEmpty(receivedMessageOriginal) && !isReceivedCountdownActive && secondsSinceLastReceivedActivity >= 2.0)
            {
                StartReceivedCountdown();
            }

            // Update received message countdown if active
            if (isReceivedCountdownActive)
            {
                UpdateReceivedCountdown();
            }

            if (secondsSinceLastReceivedActivity >= MessageDeletionDelaySeconds + 2 && !string.IsNullOrEmpty(receivedMessageOriginal))
            {
                // If printer is connected, save the message before clearing
                if (connectedPrinter != null)
                {
                    connectedPrinter.CreateTelegram(receivedMessageOriginal, NetworkUID.ToString());
                }
                
                // Always clear the message after the delay (with or without printer)
                receivedMessage = "";
                receivedMessageOriginal = "";
                isReceivedCountdownActive = false;
                receivedCountdownSeconds = 0;
                receivedCountdownEndTime = 0;
                MarkDirty();
                dialog?.UpdateReceivedText("");
                dialog?.UpdateCountdown(-1); // Hide countdown completely
            }
        }

        private void StartReceivedCountdown()
        {
            isReceivedCountdownActive = true;
            receivedCountdownSeconds = MessageDeletionDelaySeconds; // Start from the delay (e.g., 10 seconds)
            dialog?.UpdateCountdown(receivedCountdownSeconds);
        }

        private void StartSentCountdown()
        {
            isSentCountdownActive = true;
            sentCountdownSeconds = MessageDeletionDelaySeconds; // Start from the delay (e.g., 10 seconds)
            dialog?.UpdateSentCountdown(sentCountdownSeconds);
        }

        private void UpdateReceivedCountdown()
        {
            long currentTime = Api.World.ElapsedMilliseconds;
            long timeSinceLastReceivedActivity = currentTime - lastReceivedActivityTime;
            double secondsSinceLastReceivedActivity = timeSinceLastReceivedActivity / 1000.0;
            
            // Calculate countdown: starts 2 seconds after last activity, counts down for 10 seconds
            // So at 2 seconds: countdown = 10, at 12 seconds: countdown = 0
            int newCountdown = Math.Max(0, MessageDeletionDelaySeconds - (int)secondsSinceLastReceivedActivity + 2);
            
            if (newCountdown != receivedCountdownSeconds)
            {
                receivedCountdownSeconds = newCountdown;
                dialog?.UpdateCountdown(receivedCountdownSeconds);
                
                // If countdown just reached 0, record the time
                if (receivedCountdownSeconds == 0 && receivedCountdownEndTime == 0)
                {
                    receivedCountdownEndTime = currentTime;
                    
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
            if (receivedCountdownSeconds == 0 && receivedCountdownEndTime > 0)
            {
                double timeSinceCountdownEnd = (currentTime - receivedCountdownEndTime) / 1000.0;
                if (timeSinceCountdownEnd >= 2.0)
                {
                    dialog?.UpdateCountdown(-1); // Hide countdown
                    isReceivedCountdownActive = false;
                    receivedCountdownEndTime = 0;
                }
            }
        }

        private void UpdateSentCountdown()
        {
            long currentTime = Api.World.ElapsedMilliseconds;
            long timeSinceLastSentActivity = currentTime - lastSentActivityTime;
            double secondsSinceLastSentActivity = timeSinceLastSentActivity / 1000.0;
            
            // Calculate countdown: starts 2 seconds after last activity, counts down for 10 seconds
            // So at 2 seconds: countdown = 10, at 12 seconds: countdown = 0
            int newCountdown = Math.Max(0, MessageDeletionDelaySeconds - (int)secondsSinceLastSentActivity + 2);
            
            if (newCountdown != sentCountdownSeconds)
            {
                sentCountdownSeconds = newCountdown;
                dialog?.UpdateSentCountdown(sentCountdownSeconds);
                
                // If countdown just reached 0, record the time
                if (sentCountdownSeconds == 0 && sentCountdownEndTime == 0)
                {
                    sentCountdownEndTime = currentTime;
                }
            }
            
            // Clear sent message 2 seconds after countdown reached 0
            if (sentCountdownSeconds == 0 && sentCountdownEndTime > 0)
            {
                double timeSinceCountdownEnd = (currentTime - sentCountdownEndTime) / 1000.0;
                if (timeSinceCountdownEnd >= 2.0)
                {
                    // Clear the sent message
                    sentMessage = "";
                    sentMessageOriginal = "";
                    isSentCountdownActive = false;
                    sentCountdownSeconds = 0;
                    sentCountdownEndTime = 0;
                    MarkDirty();
                    dialog?.UpdateSentText("");
                    dialog?.UpdateSentCountdown(-1); // Hide countdown
                }
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            this.InitializeAnimatorWithRotation("telegraphkey");
            return this.HasActiveAnimations();
        }

        private void TriggerKeyClickAnimation()
        {
            this.PlaySingleShotAnimation("click");
        }

    }
}