using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.BlockEntityBehavior;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Systems;
using RPVoiceChat.Networking.Packets;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityTelegraph : BEWireNode, INetworkRoot, IWireTypedNode, ITelegraphEndpoint
    {
        TelegraphMenuDialog dialog;
        protected override int MaxConnections => 1;
        public override bool IsActiveEndpoint => true;

        // INetworkRoot implementation - stores the original network ID created by this root
        private long originalCreatedNetworkID = 0;
        public long CreatedNetworkID => originalCreatedNetworkID;

        public bool IsPlaying { get; private set; }
        public int Volume { get; set; } = 8;
        public bool GenuineMorseCharacters => ServerConfigManager.TelegraphGenuineMorseCharacters;
        private int MaxMessageLength => ServerConfigManager.TelegraphMaxMessageLength;

        private string sentMessage = "";
        private string receivedMessage = "";
        private string sentMessageOriginal = ""; // Store original latin characters
        private string receivedMessageOriginal = ""; // Store original latin characters
        private string customEndpointName = "";
        private string targetEndpointName = "all";
        /// <summary>Server-pushed mirror of <see cref="WireNetwork.IsManagedBySwitchboard"/>; replicated to clients via BE data.</summary>
        private bool routingManagedBySwitchboard;
        /// <summary>Server-pushed mirror of <see cref="WireNetwork.AdvancedTelegraphFeaturesEnabled"/>; replicated to clients via BE data.</summary>
        private bool routingAdvancedUnlocked;
        private string routingDisabledReasonLangKey = "Telegraph.Settings.DisabledNoPower";
        private WireRouteMode lastReceivedRouteMode = WireRouteMode.All;
        private string lastReceivedSourceEndpointName = null;
        private string lastReceivedTargetEndpointName = null;
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
        private bool telegramPrinted = false; // Flag to track if telegram has been created
        private bool printPacketSentForCurrentMessage = false;

        private RPVoiceChat.GameContent.BlockEntityBehavior.BEBehaviorAnimatable Animatable => GetBehavior<RPVoiceChat.GameContent.BlockEntityBehavior.BEBehaviorAnimatable>();
        public WireNodeKind WireNodeKind => WireNodeKind.Telegraph;
        public string CustomEndpointName => customEndpointName;

        public BlockEntityTelegraph() : base()
        {
        }

        public override void OnNetworkCreated(long networkID)
        {
            base.OnNetworkCreated(networkID);
            // Store the original network ID created by this root
            if (originalCreatedNetworkID == 0)
            {
                originalCreatedNetworkID = networkID;
                MarkDirty();
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Don't initialize originalCreatedNetworkID here - it should only be set when OnNetworkCreated is called
            // This ensures that only roots that actually created a network have originalCreatedNetworkID set

            IsPlaying = false;
            lastReceivedActivityTime = api.World.ElapsedMilliseconds;
            lastSentActivityTime = api.World.ElapsedMilliseconds;

            OnReceivedSignalEvent += HandleReceivedSignal;

            if (api.Side == EnumAppSide.Client)
            {
                Animatable?.InitializeAnimatorWithRotation("telegraphkey");
                UpdateDisplayMessages();
                
                // Register client-side countdown update timer
                api.Event.RegisterGameTickListener(OnClientGameTick, 1000); // Every second
            }
            else if (NetworkUID != 0)
            {
                WireNetworkHandler.RefreshTelegraphRoutingSnapshot(NetworkUID);
            }
            
            // Check for printer below
            CheckForPrinterBelow();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            sentMessageOriginal = tree.GetString("sentMessage");
            receivedMessageOriginal = tree.GetString("receivedMessage");
            customEndpointName = tree.GetString("customEndpointName", "");
            targetEndpointName = tree.GetString("targetEndpointName", "all");
            long savedOriginalCreatedNetworkID = tree.GetLong("originalCreatedNetworkID", 0);
            // Only restore if it's not 0 (meaning this root actually created a network)
            if (savedOriginalCreatedNetworkID != 0)
            {
                originalCreatedNetworkID = savedOriginalCreatedNetworkID;
            }
            routingManagedBySwitchboard = tree.GetBool("rpvc:routingManaged", false);
            routingAdvancedUnlocked = tree.GetBool("rpvc:routingAdvanced", false);
            routingDisabledReasonLangKey = tree.GetString("rpvc:routingDisabledReason", "Telegraph.Settings.DisabledNoPower");
            if (Api?.Side == EnumAppSide.Server && NetworkUID != 0)
            {
                WireNetworkHandler.RefreshTelegraphRoutingSnapshot(NetworkUID);
            }
            UpdateDisplayMessages();
            
            // Update dialog if it exists
            if (IsDialogOpen())
            {
                dialog?.UpdateSentText(sentMessage);
                dialog?.UpdateReceivedText(receivedMessage);
                dialog?.RefreshRoutingControls();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("sentMessage", sentMessageOriginal);
            tree.SetString("receivedMessage", receivedMessageOriginal);
            tree.SetString("customEndpointName", customEndpointName);
            tree.SetString("targetEndpointName", targetEndpointName);
            tree.SetLong("originalCreatedNetworkID", originalCreatedNetworkID);
            tree.SetBool("rpvc:routingManaged", routingManagedBySwitchboard);
            tree.SetBool("rpvc:routingAdvanced", routingAdvancedUnlocked);
            tree.SetString("rpvc:routingDisabledReason", routingDisabledReasonLangKey ?? "Telegraph.Settings.DisabledNoPower");
        }

        /// <summary>Called from <see cref="WireNetworkHandler.RefreshTelegraphRoutingSnapshot"/> on the server after the wire network state is rebuilt.</summary>
        public void ApplyServerRoutingFlags(bool managedBySwitchboard, bool advancedRoutingUnlocked, string disabledReasonLangKey = null)
        {
            if (Api?.Side != EnumAppSide.Server)
            {
                return;
            }

            bool changed = routingManagedBySwitchboard != managedBySwitchboard
                || routingAdvancedUnlocked != advancedRoutingUnlocked
                || !string.Equals(routingDisabledReasonLangKey, disabledReasonLangKey ?? "Telegraph.Settings.DisabledNoPower", StringComparison.Ordinal);
            routingManagedBySwitchboard = managedBySwitchboard;
            routingAdvancedUnlocked = advancedRoutingUnlocked;
            routingDisabledReasonLangKey = disabledReasonLangKey ?? "Telegraph.Settings.DisabledNoPower";
            if (changed)
            {
                MarkDirty(true);
            }
        }

        public bool IsManagedBySwitchboard() => routingManagedBySwitchboard;

        public bool HasAdvancedRoutingEnabled() => routingAdvancedUnlocked;

        public string GetRoutingDisabledReasonLangKey() => routingDisabledReasonLangKey ?? "Telegraph.Settings.DisabledNoPower";

        public string GetTargetEndpointName()
        {
            if (!HasAdvancedRoutingEnabled())
            {
                return "all";
            }

            if (string.IsNullOrWhiteSpace(targetEndpointName))
            {
                return "all";
            }

            return targetEndpointName;
        }

        public string[] GetAvailableEndpointNames()
        {
            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network == null)
            {
                return System.Array.Empty<string>();
            }

            var names = new List<string>();
            foreach (var endpoint in network.Nodes.ToArray().OfType<BlockEntityTelegraph>())
            {
                if (endpoint.Pos.Equals(Pos))
                    continue;

                if (string.IsNullOrWhiteSpace(endpoint.CustomEndpointName))
                    continue;

                if (!names.Contains(endpoint.CustomEndpointName))
                {
                    names.Add(endpoint.CustomEndpointName);
                }
            }

            names.Sort(System.StringComparer.OrdinalIgnoreCase);
            return names.ToArray();
        }

        public bool SetCustomEndpointName(string desiredName, out string failureLangKey)
        {
            failureLangKey = null;
            desiredName = desiredName?.Trim() ?? "";

            if (!IsManagedBySwitchboard())
            {
                return false;
            }

            if (!HasAdvancedRoutingEnabled())
            {
                failureLangKey = GetRoutingDisabledReasonLangKey();
                return false;
            }

            if (desiredName.Length == 0)
            {
                customEndpointName = "";
                MarkDirty();
                return true;
            }

            if (WireNetworkHandler.IsEndpointNameTaken(NetworkUID, desiredName, this))
            {
                failureLangKey = "Telegraph.Settings.NameAlreadyUsed";
                return false;
            }

            customEndpointName = desiredName;
            MarkDirty();

            if (!string.Equals(targetEndpointName, "all", System.StringComparison.OrdinalIgnoreCase))
            {
                var names = GetAvailableEndpointNames();
                if (!names.Contains(targetEndpointName, System.StringComparer.OrdinalIgnoreCase))
                {
                    targetEndpointName = "all";
                }
            }

            return true;
        }

        public void SetTargetEndpointName(string name)
        {
            if (!IsManagedBySwitchboard() || !HasAdvancedRoutingEnabled())
            {
                targetEndpointName = "all";
                MarkDirty();
                return;
            }

            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "all", System.StringComparison.OrdinalIgnoreCase))
            {
                targetEndpointName = "all";
                MarkDirty();
                return;
            }

            var names = GetAvailableEndpointNames();
            if (names.Contains(name, System.StringComparer.OrdinalIgnoreCase))
            {
                targetEndpointName = name;
            }
            else
            {
                targetEndpointName = "all";
            }

            MarkDirty();
        }

        protected override void SetWireAttachmentOffset()
        {
            WireAttachmentOffset = new Vec3f(0.5f, 0.1f, 0.5f);
        }

        public bool OnInteract()
        {
            if (Api.Side == EnumAppSide.Server)
                return true;

            if (Api is not ICoreClientAPI capi)
                return true;

            if (dialog?.IsOpened() == true)
            {
                return true;
            }

            // Recreate the GUI each open to avoid stale control references
            // when power mode switches between editable and read-only.
            dialog = new TelegraphMenuDialog(capi, this);

            // Update display messages before opening dialog
            UpdateDisplayMessages();
            MarkDirty(); // Mark as dirty after updating display messages
            dialog?.UpdateSentText(sentMessage);
            dialog?.UpdateReceivedText(receivedMessage);
            dialog?.RefreshRoutingControls();
            
            dialog.TryOpen();
            return true;
        }

        public void RequestSaveCustomEndpointName(string desiredName)
        {
            if (Api.Side != EnumAppSide.Client) return;
            RPVoiceChatMod.TelegraphSettingsClientChannel?.SendPacket(new TelegraphSettingsPacket
            {
                TelegraphPos = Pos,
                Operation = TelegraphSettingsOperation.SetCustomName,
                Value = desiredName ?? ""
            });
        }

        public void RequestTargetEndpointChange(string targetName)
        {
            if (Api.Side != EnumAppSide.Client) return;
            RPVoiceChatMod.TelegraphSettingsClientChannel?.SendPacket(new TelegraphSettingsPacket
            {
                TelegraphPos = Pos,
                Operation = TelegraphSettingsOperation.SetTarget,
                Value = targetName ?? "all"
            });
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

            // Check length on original message, not the morse version
            if (sentMessageOriginal.Length >= MaxMessageLength)
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

            sentMessageOriginal += keyChar.ToString(); // Store original latin character
            UpdateDisplayMessages(); // Rebuild display messages from original
            MarkDirty();
            if (IsDialogOpen()) dialog?.UpdateSentText(sentMessage);
            
            TriggerKeyClickAnimation();

            if (!string.IsNullOrEmpty(messageToSend))
            {
                string resolvedTarget = GetTargetEndpointName();
                WireRouteMode routeMode = string.Equals(resolvedTarget, "all", StringComparison.OrdinalIgnoreCase)
                    ? WireRouteMode.All
                    : WireRouteMode.NamedEndpoint;

                clientApi.Network.GetChannel(WireNetworkHandler.NetworkChannel)
                    .SendPacket(new WireNetworkMessage()
                    {
                        NetworkUID = NetworkUID,
                        Message = messageToSend,
                        SenderPos = Pos,
                        SenderPlayerUID = clientApi.World.Player?.PlayerUID,
                        RouteMode = routeMode,
                        TargetEndpointName = routeMode == WireRouteMode.NamedEndpoint ? resolvedTarget : null
                    });
            }

            if (!IsPlaying)
            {
                _ = ProcessNextSignalAsync();
            }
        }

        public void OnReceivedSignal(char keyChar, BlockPos senderPos)
        {
            if (Api.Side != EnumAppSide.Client)
                return;

            UpdateActivityTime();

            if (Api is ICoreClientAPI clientApi)
            {
                receivedMessageOriginal += keyChar.ToString();
                UpdateDisplayMessages();
                MarkDirty();
                if (IsDialogOpen()) dialog?.UpdateReceivedText(receivedMessage);
                printPacketSentForCurrentMessage = false;
                if (Pos.Equals(senderPos))
                    TriggerKeyClickAnimation();
            }

            Task.Run(() => PlayMorseAsync(ConvertKeyCodeToMorse(keyChar)));
        }


        private void HandleReceivedSignal(object sender, WireNetworkMessage e)
        {
            if (e?.Message == null || e.Message.Length == 0) return;

            lastReceivedRouteMode = e.RouteMode;
            lastReceivedSourceEndpointName = ResolveTelegraphNameAt(e.SenderPos);
            lastReceivedTargetEndpointName = e.TargetEndpointName;
            char keyChar = e.Message[0];
            OnReceivedSignal(keyChar, e.SenderPos);
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

        public void ProcessPrintPacket(string message, string sourceEndpointName = null, string targetEndpointName = null, string networkName = null)
        {
            // Trigger message printing/deletion
            if (connectedPrinter != null && !string.IsNullOrEmpty(message))
            {
                // Convert to morse for printing if GenuineMorseCharacters is enabled
                string messageToPrint = GenuineMorseCharacters ? ConvertStringToMorse(message) : message;
                string networkUid = NetworkUID.ToString();
                string resolvedNetworkName = !string.IsNullOrWhiteSpace(networkName) ? networkName : ResolvePrintableNetworkName();
                connectedPrinter.CreateTelegram(messageToPrint, networkUid, resolvedNetworkName, sourceEndpointName, targetEndpointName);
                telegramPrinted = true; // Mark that telegram has been created
            }
            
            // Clear the message - CheckAutoSave will also clean it up if ProcessPrintPacket didn't handle it
            receivedMessage = "";
            receivedMessageOriginal = "";
            isReceivedCountdownActive = false;
            receivedCountdownSeconds = 0;
            receivedCountdownEndTime = 0;
            printPacketSentForCurrentMessage = false;
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

        public string GetCustomEndpointName()
        {
            return customEndpointName ?? "";
        }

        public void ClearMessages()
        {
            sentMessage = "";
            receivedMessage = "";
            sentMessageOriginal = "";
            receivedMessageOriginal = "";
            MarkDirty();
            pendingSignals.Clear();
            if (IsDialogOpen())
            {
                dialog?.UpdateSentText("");
                dialog?.UpdateReceivedText("");
            }
            
            // Reset countdowns
            isReceivedCountdownActive = false;
            receivedCountdownSeconds = 0;
            receivedCountdownEndTime = 0;
            isSentCountdownActive = false;
            sentCountdownSeconds = 0;
            sentCountdownEndTime = 0;
            telegramPrinted = false; // Reset flag
            printPacketSentForCurrentMessage = false;
            if (IsDialogOpen())
            {
                dialog?.UpdateCountdown(-1);
                dialog?.UpdateSentCountdown(-1);
            }
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

        private string ConvertStringToMorse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            StringBuilder morse = new StringBuilder();
            foreach (char c in text)
            {
                string morseChar = ConvertKeyCodeToMorse(c);
                if (!string.IsNullOrEmpty(morseChar))
                {
                    morse.Append(morseChar);
                }
            }
            return morse.ToString();
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
            if (!string.IsNullOrEmpty(receivedMessageOriginal) && !isReceivedCountdownActive && !printPacketSentForCurrentMessage && secondsSinceLastReceivedActivity >= 2.0)
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
            if (!string.IsNullOrEmpty(receivedMessageOriginal) && !isReceivedCountdownActive && !printPacketSentForCurrentMessage && secondsSinceLastReceivedActivity >= 2.0)
            {
                StartReceivedCountdown();
            }

            // Update received message countdown if active
            if (isReceivedCountdownActive)
            {
                UpdateReceivedCountdown();
            }

            if (secondsSinceLastReceivedActivity >= MessageDeletionDelaySeconds + 2 && (!string.IsNullOrEmpty(receivedMessageOriginal) || telegramPrinted))
            {
                // Telegram creation is exclusively handled by ProcessPrintPacket
                // Just clear the message and reset flag here
                receivedMessage = "";
                receivedMessageOriginal = "";
                isReceivedCountdownActive = false;
                receivedCountdownSeconds = 0;
                receivedCountdownEndTime = 0;
                telegramPrinted = false; // Reset flag for next message
                printPacketSentForCurrentMessage = false;
                MarkDirty();
                if (IsDialogOpen())
                {
                    dialog?.UpdateReceivedText("");
                    dialog?.UpdateCountdown(-1); // Hide countdown completely
                }
            }
        }

        private void StartReceivedCountdown()
        {
            isReceivedCountdownActive = true;
            receivedCountdownSeconds = MessageDeletionDelaySeconds; // Start from the delay (e.g., 10 seconds)
            if (IsDialogOpen()) dialog?.UpdateCountdown(receivedCountdownSeconds);
        }

        private void StartSentCountdown()
        {
            isSentCountdownActive = true;
            sentCountdownSeconds = MessageDeletionDelaySeconds; // Start from the delay (e.g., 10 seconds)
            if (IsDialogOpen()) dialog?.UpdateSentCountdown(sentCountdownSeconds);
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
                if (IsDialogOpen()) dialog?.UpdateCountdown(receivedCountdownSeconds);
                
                // If countdown just reached 0, record the time
                if (receivedCountdownSeconds == 0 && receivedCountdownEndTime == 0)
                {
                    receivedCountdownEndTime = currentTime;
                    
                    // Send packet to server to trigger printing
                    if (Api.Side == EnumAppSide.Client && !printPacketSentForCurrentMessage && !string.IsNullOrEmpty(receivedMessageOriginal))
                    {
                        var packet = new CommDeliveryPacket
                        {
                            DevicePos = Pos,
                            PayloadType = CommPayloadType.Text,
                            TextMessage = receivedMessageOriginal,
                            NetworkName = ResolvePrintableNetworkName(),
                            // Always carry sender display name for telegram headers (broadcast or named route).
                            SourceEndpointName = lastReceivedSourceEndpointName,
                            TargetEndpointName = lastReceivedRouteMode == WireRouteMode.NamedEndpoint
                                ? lastReceivedTargetEndpointName
                                : null
                        };
                        RPVoiceChatMod.TelegraphPrintClientChannel.SendPacket(packet);
                        printPacketSentForCurrentMessage = true;
                    }
                }
            }
            
            // Hide countdown 2 seconds after it reached 0
            if (receivedCountdownSeconds == 0 && receivedCountdownEndTime > 0)
            {
                double timeSinceCountdownEnd = (currentTime - receivedCountdownEndTime) / 1000.0;
                if (timeSinceCountdownEnd >= 2.0)
                {
                    if (IsDialogOpen()) dialog?.UpdateCountdown(-1); // Hide countdown
                    isReceivedCountdownActive = false;
                    receivedCountdownEndTime = 0;
                }
            }
        }

        private string ResolveTelegraphNameAt(BlockPos nodePos)
        {
            if (nodePos == null)
            {
                return null;
            }

            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network == null)
            {
                return null;
            }

            var endpoint = network.Nodes
                .OfType<BlockEntityTelegraph>()
                .FirstOrDefault(t => t.Pos != null && t.Pos.Equals(nodePos));

            if (endpoint == null)
            {
                return null;
            }

            string name = endpoint.GetCustomEndpointName();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private string ResolvePrintableNetworkName()
        {
            if (!routingAdvancedUnlocked)
            {
                return null;
            }

            string displayName = WireNetworkHandler.GetDisplayName(NetworkUID);
            if (!string.Equals(displayName, NetworkUID.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return displayName;
            }

            return null;
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
                if (IsDialogOpen()) dialog?.UpdateSentCountdown(sentCountdownSeconds);
                
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
                    if (IsDialogOpen())
                    {
                        dialog?.UpdateSentText("");
                        dialog?.UpdateSentCountdown(-1); // Hide countdown
                    }
                }
            }
        }

        private void TriggerKeyClickAnimation()
        {
            Animatable?.PlaySingleShotAnimation("click");
        }

        private bool IsDialogOpen()
        {
            return dialog != null && dialog.IsOpened();
        }

    }
}