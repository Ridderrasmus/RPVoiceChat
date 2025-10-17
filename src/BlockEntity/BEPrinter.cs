using System.Collections.Generic;
using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityPrinter : BlockEntity
    {
        private ItemSlot paperSlipSlot;
        private ItemSlot telegramSlot;
        private TelegraphBlockEntity connectedTelegraph;

        public BlockEntityPrinter() : base()
        {
            paperSlipSlot = new ItemSlot(this);
            telegramSlot = new ItemSlot(this);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            
            // Check for telegraph block above
            CheckForTelegraphAbove();
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

        private void CheckForTelegraphAbove()
        {
            if (Api?.World?.BlockAccessor == null) return;

            BlockPos abovePos = Pos.UpCopy();
            BlockEntity aboveEntity = Api.World.BlockAccessor.GetBlockEntity(abovePos);
            
            if (aboveEntity is TelegraphBlockEntity telegraph)
            {
                ConnectToTelegraph(telegraph);
            }
        }

        private void ConnectToTelegraph(TelegraphBlockEntity telegraph)
        {
            connectedTelegraph = telegraph;
            telegraph.SetPrinter(this);
        }

        private void DisconnectFromTelegraph()
        {
            if (connectedTelegraph != null)
            {
                connectedTelegraph.SetPrinter(null);
                connectedTelegraph = null;
            }
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            if (Api.Side == EnumAppSide.Server)
            {
                // Server-side: handle item interactions
                if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Code.ToShortString() == "rpvoicechat:paperslip")
                {
                    // Try to add paper slip
                    if (paperSlipSlot.Empty)
                    {
                        ItemStack paperSlip = byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                        paperSlipSlot.Itemstack = paperSlip;
                        MarkDirty();
                        return true;
                    }
                }
                else if (!telegramSlot.Empty)
                {
                    // Try to give telegram to player
                    if (byPlayer.InventoryManager.TryGiveItemstack(telegramSlot.Itemstack))
                    {
                        telegramSlot.Itemstack = null;
                        MarkDirty();
                        return true;
                    }
                }
            }

            return true;
        }

        public bool TryConsumePaperSlip()
        {
            if (!paperSlipSlot.Empty)
            {
                paperSlipSlot.Itemstack.StackSize--;
                if (paperSlipSlot.Itemstack.StackSize <= 0)
                {
                    paperSlipSlot.Itemstack = null;
                }
                MarkDirty();
                return true;
            }
            return false;
        }

        public void CreateTelegram(string message)
        {
            if (telegramSlot.Empty && TryConsumePaperSlip())
            {
                ItemStack telegram = new ItemStack(Api.World.GetItem(new AssetLocation("rpvoicechat:telegram")));
                
                // Store the message in the telegram's attributes
                ITreeAttribute telegramAttrs = telegram.Attributes.GetOrAddTreeAttribute("telegram");
                telegramAttrs.SetString("message", message);
                telegramAttrs.SetLong("timestamp", Api.World.Calendar.TotalDays);
                
                telegramSlot.Itemstack = telegram;
                MarkDirty();
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            
            paperSlipSlot.FromTreeAttributes(tree.GetTreeAttribute("paperSlipSlot"));
            telegramSlot.FromTreeAttributes(tree.GetTreeAttribute("telegramSlot"));
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            
            tree.SetTreeAttribute("paperSlipSlot", paperSlipSlot.ToTreeAttributes());
            tree.SetTreeAttribute("telegramSlot", telegramSlot.ToTreeAttributes());
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            DisconnectFromTelegraph();
        }
    }
}
