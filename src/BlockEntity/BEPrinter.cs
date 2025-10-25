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
using Vintagestory.API.Client.Tesselation;
using RPVoiceChat.GameContent.Inventory;
using Vintagestory.API.Config;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityPrinter : BlockEntityOpenableContainer
    {
        private InventoryPrinter inventory;
        private const int PaperSlotId = 0;
        
        public string dialogTitleLangCode = "printercontents";
        public virtual string DialogTitle => "Printer Inventory";
        
        // Animation state
        private bool isDrawerOpen = false;
        public bool IsDrawerOpen => isDrawerOpen;
        
        // Animation utility (using VS API BEBehaviorAnimatable)
        public BlockEntityAnimationUtil animUtil { get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; } }

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
            
            // Save animation state
            tree.SetBool("isDrawerOpen", isDrawerOpen);
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
            
            // Load animation state
            isDrawerOpen = tree.GetBool("isDrawerOpen", false);
            
            // Now call LateInitInventory after Pos is set by base.FromTreeAttributes
            if (inventory != null)
            {
                LateInitInventory();
            }
        }
        
        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (inventory == null) return false;
            
            // Toggle drawer state and trigger animation
            ToggleDrawerState(byPlayer, !isDrawerOpen);
            
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

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumBlockContainerPacketId.OpenInventory)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                if (capi == null) return;
                
                string dialogClassName;
                string dialogTitle;
                int cols;
                TreeAttribute tree = new TreeAttribute();
                
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    dialogClassName = reader.ReadString();
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
                    
                    var invDialog = new GuiDialogBlockEntityInventory(dialogTitle, inventory, Pos, cols, capi);
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
            
            // Play sound with pitch variation like trapdoor
            float pitch = isDrawerOpen ? 1.1f : 0.9f;
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

        public bool StoreTelegram(string message, string networkUID = "")
        {
            // Try to find an empty telegram slot
            foreach (var slot in inventory.TelegramSlots)
            {
                if (slot.Empty)
                {
                    bool result = slot.TryStoreTelegram(message, networkUID);
                    if (result)
                    {
                        MarkDirty(); // Mark block entity as dirty when inventory changes
                        return true;
                    }
                }
            }
            return false;
        }

        public void CreateTelegram(string message, string networkUID = "")
        {
            if (Api.Side == EnumAppSide.Server)
            {
                if (StoreTelegram(message, networkUID))
                {
                    // Consume paper only when telegram is actually stored
                    if (ConsumePaperSlip())
                    {
                        Api.Logger.Event("Telegram printed: " + message);
                    }
                    else
                    {
                        Api.Logger.Warning("Printer has no paper slips.");
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
                else
                {
                    Api.Logger.Warning("Printer has no empty slot for telegram.");
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            // Don't call base.GetBlockInfo() to avoid showing food preservation properties
            // Just show basic printer info
            dsc.AppendLine("Printer");
            if (inventory != null)
            {
                if (!inventory.PaperSlot.Empty)
                {
                    dsc.AppendLine($"Paper: {inventory.PaperSlot.Itemstack.StackSize}");
                }
                int telegramCount = 0;
                foreach (var slot in inventory.TelegramSlots)
                {
                    if (!slot.Empty) telegramCount++;
                }
                if (telegramCount > 0)
                {
                    dsc.AppendLine($"Telegrams: {telegramCount}/9");
                }
            }
        }

        // Properties for easy access to slots
        public SlotPrinterPaper PaperSlot => inventory.PaperSlot;
        public SlotPrinterTelegram[] TelegramSlots => inventory.TelegramSlots;

        /// <summary>
        /// Trigger an animation by name
        /// </summary>
        /// <param name="animationName">Name of the animation to trigger</param>
        public void TriggerAnimation(string animationName)
        {
            if (animUtil != null)
            {
                // Check if animation is already running
                if (animUtil.activeAnimationsByAnimCode.ContainsKey(animationName))
                {
                    return;
                }
                
                animUtil.StartAnimation(new AnimationMetaData() 
                { 
                    Animation = animationName, 
                    Code = animationName 
                });
                
                // Play sound effect
                playDrawerSound();
            }
        }

        /// <summary>
        /// Trigger drawer animation (like trapdoor ToggleDoorWing)
        /// </summary>
        /// <param name="open">True to open drawer, false to close</param>
        private void TriggerDrawerAnimation(bool open)
        {
            if (!open)
            {
                // Closing: stop the animation
                animUtil.StopAnimation("open-drawer");
            }
            else
            {
                // Opening: start the animation
                animUtil.StartAnimation(new AnimationMetaData() 
                { 
                    Animation = "open-drawer", 
                    Code = "open-drawer" 
                });
            }
        }


        private void playDrawerSound()
        {
            Api.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/block/printer/drawer.ogg"),
                Pos,
                0,
                null,
                false,
                12,
                0.75f
            );
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            // Initialize animator here, following the Sprinkler Mod example
            if (animUtil?.animator == null)
            {
                animUtil?.InitializeAnimator("printer");
            }
            
            return animUtil?.activeAnimationsByAnimCode.Count > 0 || (animUtil?.animator != null && animUtil.animator.ActiveAnimationCount > 0);
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