using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Client;
using RPVoiceChat.GameContent.BlockEntityBehavior;
using RPVoiceChat.GameContent.Inventory;
using RPVoiceChat.Gui;
using RPVoiceChat.Util;
using Vintagestory.API.Config;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityPrinter : BlockEntityOpenableContainer
    {
        private InventoryPrinter inventory;

        /// <summary>Full inventory sync like BlockEntityLucerne.PacketIdInventory; custom dialog uses AddItemSlotGrid(..., "slots").</summary>
        public static readonly int PacketIdPrinterInventory = GetPrinterPacketIdBase() + 0;

        /// <summary>Client → server: inventory closed, drawer must be closed on server so tree sync does not reopen it for others.</summary>
        public static readonly int PacketIdPrinterDrawerClose = GetPrinterPacketIdBase() + 1;

        private const string TreeKeyDrawerOpen = "rpvcDrawerOpen";

        private static int GetPrinterPacketIdBase()
        {
            try
            {
                var enumType = typeof(EnumBlockContainerPacketId);
                if (enumType == null || !enumType.IsEnum) return 10000;
                var values = Enum.GetValues(enumType);
                int max = 0;
                foreach (var v in values) { int i = (int)v; if (i > max) max = i; }
                // Offset after lucerne's +2 so no collision with other mod BEs using same pattern
                return max + 10;
            }
            catch { return 10010; }
        }
        
        public virtual string DialogTitle => Lang.Get($"{RPVoiceChatMod.modID}:Printer.Contents");
        
        // Animation state
        private bool isDrawerOpen = false;
        public bool IsDrawerOpen => isDrawerOpen;
        private bool isPrinterDialogOpenClient = false;
        /// <summary>Client: millis when the player right-clicked the printer; consumed when we actually open the inventory dialog so duplicate OpenInventory packets cannot reopen the drawer.</summary>
        private long lastClientInventoryOpenRequestMs = -1L;
        
        
        private RPVoiceChat.GameContent.BlockEntityBehavior.BEBehaviorAnimatable Animatable => GetBehavior<RPVoiceChat.GameContent.BlockEntityBehavior.BEBehaviorAnimatable>();

        public BlockEntityPrinter()
        {
        }

        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "printer";

        public override void Initialize(ICoreAPI api)
        {
            // Create inventory if it doesn't exist yet (pattern from BEGenericTypedContainer)
            if (inventory == null)
            {
                InitInventory(api);
            }
            
            base.Initialize(api);
            
            // Late initialize inventory after base initialization
            LateInitInventory();
            
            if (api.Side == EnumAppSide.Client)
                Animatable?.InitializeAnimatorWithRotation("printer");
            
            // Check for telegraph block above
            CheckForTelegraphAbove();
        }
        
        protected virtual void InitInventory(ICoreAPI api)
        {
            inventory = new InventoryPrinter(api, Pos);
        }
        
        public virtual void LateInitInventory()
        {
            Inventory.LateInitialize(InventoryClassName + "-" + Pos, Api);
            Inventory.ResolveBlocksOrItems();
            Inventory.Pos ??= Pos;
            MarkDirty();
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            isDrawerOpen = false; // Ensure drawer starts closed
            CheckForTelegraphAbove();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            DisconnectFromTelegraph();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            DisconnectFromTelegraph();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            
            if (inventory != null)
            {
                TreeAttribute invTree = new TreeAttribute();
                inventory.ToTreeAttributes(invTree);
                tree["inventory"] = invTree;
            }

            tree.SetBool(TreeKeyDrawerOpen, isDrawerOpen);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            if (Api == null) this.Api = worldForResolving.Api;
            
            // Create inventory BEFORE calling base.FromTreeAttributes
            if (inventory == null)
            {
                InitInventory(worldForResolving.Api);
            }
            
            // Now call base.FromTreeAttributes with inventory already created
            base.FromTreeAttributes(tree, worldForResolving);
            
            // Load inventory data if it exists
            if (tree["inventory"] != null)
            {
                inventory.FromTreeAttributes(tree["inventory"] as TreeAttribute);
                inventory.ResolveBlocksOrItems();
            }

            ApplyDrawerStateFromTree(tree);
            
            // Now call LateInitInventory after Pos is set by base.FromTreeAttributes
            if (inventory != null)
            {
                LateInitInventory();
            }
        }

        /// <summary>
        /// Sync drawer open state from server tree. Only runs drawer animation when the value <i>changes</i> on the client,
        /// so inventory/print <see cref="MarkDirty"/> resyncs do not retrigger the drawer anim.
        /// Sounds for open/close are handled by the interaction path (server plays drawer sound on close packet).
        /// </summary>
        private void ApplyDrawerStateFromTree(ITreeAttribute tree)
        {
            bool incoming = tree.GetBool(TreeKeyDrawerOpen, false);
            bool prev = isDrawerOpen;
            isDrawerOpen = incoming;

            if (Api?.Side != EnumAppSide.Client || prev == incoming)
            {
                return;
            }

            TriggerDrawerAnimation(isDrawerOpen);
        }
        
        /// <summary>Called from <see cref="PrinterBlock"/> on the client before the server handles the click so we can correlate OpenInventory packets with a real interaction.</summary>
        public void NotifyClientInventoryOpenIntent()
        {
            if (Api?.Side == EnumAppSide.Client)
            {
                lastClientInventoryOpenRequestMs = Api.World.ElapsedMilliseconds;
            }
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api.World is IClientWorldAccessor)
            {
                NotifyClientInventoryOpenIntent();
            }

            // Only toggle if drawer is currently closed
            if (!isDrawerOpen)
            {
                // Toggle drawer state and trigger animation
                ToggleDrawerState(byPlayer, !isDrawerOpen);
            }
            
            if (Api.World is IServerWorldAccessor)
            {
                // Send packet to client to open inventory (simplified approach)
                byte[] data;
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    writer.Write("BlockEntityInventory");
                    writer.Write(DialogTitle);
                    writer.Write((byte)5); // 5 columns for 10 slots
                    TreeAttribute tree = new TreeAttribute();
                    inventory.ToTreeAttributes(tree);
                    tree.ToBytes(writer);
                    data = ms.ToArray();
                }
                
                ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer, Pos, (int)EnumBlockContainerPacketId.OpenInventory, data);
                byPlayer.InventoryManager.OpenInventory(inventory);
            }
            return true;
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            if (packetid == PacketIdPrinterDrawerClose)
            {
                if (fromPlayer?.Entity?.Pos == null)
                {
                    return;
                }

                if (fromPlayer.Entity.Pos.SquareDistanceTo(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5) > 6 * 6)
                {
                    return;
                }

                if (!isDrawerOpen)
                {
                    return;
                }

                isDrawerOpen = false;
                playDrawerSound();
                MarkDirty(true);
                return;
            }

            if (packetid == PacketIdPrinterInventory && data != null && data.Length > 0 && inventory != null)
            {
                try
                {
                    var tree = new TreeAttribute();
                    using (var ms = new MemoryStream(data))
                        tree.FromBytes(new BinaryReader(ms));
                    inventory.FromTreeAttributes(tree);
                    inventory.ResolveBlocksOrItems();
                    MarkDirty();
                }
                catch { /* ignore */ }
                return;
            }
            base.OnReceivedClientPacket(fromPlayer, packetid, data);
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumBlockContainerPacketId.OpenInventory)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                if (capi == null) return;
                
                string dialogTitle;
                int cols;
                TreeAttribute tree = new TreeAttribute();
                
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    string packetType = reader.ReadString();
                    if (!string.Equals(packetType, "BlockEntityInventory", StringComparison.Ordinal))
                    {
                        return;
                    }
                    dialogTitle = reader.ReadString();
                    cols = reader.ReadByte();
                    tree.FromBytes(reader);
                }

                // Create inventory if it doesn't exist on client side
                if (inventory == null)
                {
                    InitInventory(Api);
                    LateInitInventory();
                }
                
                if (inventory != null)
                {
                    inventory.FromTreeAttributes(tree);
                    inventory.ResolveBlocksOrItems();

                    long nowMs = Api.World.ElapsedMilliseconds;
                    bool recentLocalRequest = lastClientInventoryOpenRequestMs >= 0L
                        && nowMs - lastClientInventoryOpenRequestMs <= 1500L;
                    if (!recentLocalRequest || isPrinterDialogOpenClient)
                    {
                        return;
                    }

                    // One open per click: consume intent so a second OpenInventory (e.g. inventory resync) does not pop the GUI again.
                    lastClientInventoryOpenRequestMs = -1L;

                    isPrinterDialogOpenClient = true;
                    var invDialog = new PrinterInventoryDialog(dialogTitle, inventory, Pos, cols, capi, () => {
                        isPrinterDialogOpenClient = false;
                        isDrawerOpen = false;
                        TriggerDrawerAnimation(false);
                        capi.Network.SendBlockEntityPacket(Pos, PacketIdPrinterDrawerClose, Array.Empty<byte>());
                    });
                    
                    invDialog.TryOpen();
                }
            }
        }

        /// <summary>
        /// Toggle drawer state
        /// </summary>
        private void ToggleDrawerState(IPlayer byPlayer, bool newState)
        {
            this.isDrawerOpen = newState;
            TriggerDrawerAnimation(isDrawerOpen);
            
            playDrawerSound();
            
            MarkDirty(true);
            
            if (Api?.Side == EnumAppSide.Server)
            {
                Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
            }
        }



        private void CheckForTelegraphAbove()
        {
            if (Api?.World?.BlockAccessor == null) return;

            BlockPos abovePos = Pos.UpCopy();
            Vintagestory.API.Common.BlockEntity aboveEntity = Api.World.BlockAccessor.GetBlockEntity(abovePos);
            
            if (aboveEntity is BlockEntityTelegraph telegraph)
            {
                ConnectToTelegraph(telegraph);
            }
        }

        private void ConnectToTelegraph(BlockEntityTelegraph telegraph)
        {
            telegraph.SetPrinter(this);
        }

        private void DisconnectFromTelegraph()
        {
            // The telegraph will handle disconnection
        }

        public bool ConsumePaperSlip()
        {
            bool result = inventory.PaperSlot.TryConsumePaperSlip();
            if (result)
            {
                MarkDirty(); // Mark block entity as dirty when inventory changes
            }
            return result;
        }

        public bool StoreTelegram(string message, string networkUID = "", string networkName = "", string sourceEndpointName = "", string targetEndpointName = "")
        {
            // Try to find an empty telegram slot
            foreach (var slot in inventory.TelegramSlots)
            {
                if (slot.Empty)
                {
                    bool result = slot.TryStoreTelegram(message, networkUID, networkName, sourceEndpointName, targetEndpointName);
                    if (result)
                    {
                        MarkDirty(); // Mark block entity as dirty when inventory changes
                        return true;
                    }
                }
            }
            return false;
        }

        public void CreateTelegram(string message, string networkUID = "", string networkName = "", string sourceEndpointName = "", string targetEndpointName = "")
        {
            if (Api.Side == EnumAppSide.Server)
            {
                if (StoreTelegram(message, networkUID, networkName, sourceEndpointName, targetEndpointName))
                {
                    // Consume paper only when telegram is actually stored
                    if (ConsumePaperSlip())
                    {
                        playPrintSound();
                    }
                    else
                    {
                        // Remove the telegram if we can't consume paper
                        foreach (var slot in inventory.TelegramSlots)
                        {
                            if (!slot.Empty)
                            {
                                slot.Itemstack = null;
                                slot.MarkDirty();
                                break;
                            }
                        }
                    }
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            // Don't call base.GetBlockInfo() to avoid showing food preservation properties
            // Show basic printer info only
            if (inventory != null)
            {
                bool needsPaper = PaperSlot.Empty;
                bool isFull = !HasEmptyTelegramSlot();
                
                if (needsPaper)
                {
                    dsc.AppendLine(UIUtils.I18n("Printer.Warning.NoPaper"));
                }
                
                if (isFull)
                {
                    dsc.AppendLine(UIUtils.I18n("Printer.Warning.Full"));
                }
                
                if (!inventory.PaperSlot.Empty)
                {
                    dsc.AppendLine(UIUtils.I18n("Printer.Paper", inventory.PaperSlot.Itemstack.StackSize));
                }
                
                int telegramCount = 0;
                foreach (var slot in inventory.TelegramSlots)
                {
                    if (!slot.Empty) telegramCount++;
                }
                if (telegramCount > 0)
                {
                    dsc.AppendLine(UIUtils.I18n("Printer.Telegrams", telegramCount, 9));
                }
            }
        }

        // Properties for easy access to slots
        public SlotPrinterPaper PaperSlot => inventory.PaperSlot;
        public SlotPrinterTelegram[] TelegramSlots => inventory.TelegramSlots;

        private bool HasEmptyTelegramSlot()
        {
            foreach (var slot in inventory.TelegramSlots)
            {
                if (slot.Empty)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Trigger an animation by name
        /// </summary>
        /// <param name="animationName">Name of the animation to trigger</param>
        public void TriggerAnimation(string animationName)
        {
            Animatable?.StartAnimationIfNotRunning(animationName);
            playDrawerSound();
        }

        /// <summary>
        /// Trigger drawer animation (like trapdoor ToggleDoorWing)
        /// </summary>
        /// <param name="open">True to open drawer, false to close</param>
        private void TriggerDrawerAnimation(bool open)
        {
            if (!open)
                Animatable?.StopAnimation("open-drawer");
            else
                Animatable?.StartAnimationIfNotRunning("open-drawer");
        }


        private void playDrawerSound()
        {
            Api.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/block/printer/drawer.ogg"),
                Pos,
                0,
                null,
                false,
                6,
                0.7f
            );
        }

        private void playPrintSound()
        {
            Api.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/block/printer/print.ogg"),
                Pos,
                0,
                null,
                false,
                12,
                0.65f
            );
        }

        public void CloseDrawer()
        {
            if (isDrawerOpen)
            {
                isDrawerOpen = false;
                TriggerDrawerAnimation(false);
                MarkDirty(true);
            }
        }


    }
}