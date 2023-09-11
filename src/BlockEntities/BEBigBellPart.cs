using RPVoiceChat.BlockEntityRenderers;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.BlockEntities
{
    public class BlockEntityBigBellPart : BlockEntityContainer
    {
        public MeshRef BaseModelRef;
        public MeshRef[] BigBellPartMeshRef = new MeshRef[3];
        public MeshRef[] FluxMeshRef = new MeshRef[3];

        InventoryGeneric inv;

        public ItemSlot[] BellPartSlots = new ItemSlot[4];

        public int hammerHits;
        BigBellPartRenderer renderer;

        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "bigbellpart";

        public BlockEntityBigBellPart()
        {
            inv = new InventoryGeneric(5, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI capi)
            {
                renderer = new BigBellPartRenderer(capi, this);
                updateMeshRefs();
            }
        }

        void updateMeshRefs()
        {
            if (Api.Side == EnumAppSide.Server) return;
            
            var capi = Api as ICoreClientAPI;

            BaseModelRef = capi.TesselatorManager.GetDefaultBlockMeshRef(Block);

            // If the inventory contains flux, then we want to render the flux mesh
            for (int i = 0; i < BellPartSlots.Length; i++)
            {
                if (BellPartSlots[i].Empty) continue;

                MeshData meshdata;
                capi.Tesselator.TesselateShape(Block, Shape.TryGet(Api, "shapes/block/bigbellparts/bottomrim/bigbellbotweld1.json"), out meshdata);
                meshdata.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0, 0.25f * (1 + i), 0f);
                FluxMeshRef[i] = capi.Render.UploadMesh(meshdata);
            }

            // If the inventory contains the big bell parts, then we want to render the big bell part mesh
            for (int i = 0; i < inv[1].StackSize; i++)
            {
                MeshData meshdata;
                capi.Tesselator.TesselateShape(Block, Shape.TryGet(Api, "shapes/block/bigbellparts/bottomrim/bigbellbotpart.json"), out meshdata);
                meshdata.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0, 0.25f * (1 + i), 0f);
                BigBellPartMeshRef[i] = capi.Render.UploadMesh(meshdata);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                inv[0].Itemstack = byItemStack.Clone();
                inv[0].Itemstack.StackSize = 1;
            }
        }

        public void OnHammerHitOver(IPlayer byPlayer, Vec3d hitPosition)
        {
            if (inv[1].Empty || inv[2].Empty || inv[3].Empty || inv[4].Empty || !TestReadyToMerge(false)) return;

            hammerHits++;

            if (Api.Side == EnumAppSide.Client)
            {
                updateMeshRefs();
            }
        }

        private bool TestReadyToMerge(bool triggerMessage = true)
        {
            return false;

            var itemstack1 = inv[0].Itemstack;
            var itemstack2 = inv[1].Itemstack;
            var itemstack3 = inv[2].Itemstack;
            var itemstack4 = inv[3].Itemstack;
            var borax = inv[4].Itemstack;

            if (itemstack1.Collectible.GetTemperature(Api.World, itemstack1) < 550 || itemstack2.Collectible.GetTemperature(Api.World, itemstack2) < 550 || itemstack3.Collectible.GetTemperature(Api.World, itemstack3) < 550 || itemstack4.Collectible.GetTemperature(Api.World, itemstack4) < 550)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(capi.World.Player, "toocold", "Some of the parts are too cold to weld");
                }
                return false;
            }

            if (inv[4].Empty)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(capi.World.Player, "missingflux", "You need to add powdered borax to the weld as flux");
                }
                return false;
            }

            if (inv[1].Empty || inv[2].Empty || inv[3].Empty)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(capi.World.Player, "missingparts", "You need to add all the parts to the weld");
                }
                return false;
            }

            return true;
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot hotbarslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ICoreClientAPI capi = Api as ICoreClientAPI;

            if (hotbarslot.Itemstack.Collectible.Code.Path == "powderedborax")
            {
                if (inv[4].Empty)
                {
                    inv[4].Itemstack = hotbarslot.TakeOut(1);
                    return true;
                } else if (inv[4].Itemstack.StackSize < 3)
                {
                    inv[4].Itemstack.StackSize++;
                    hotbarslot.TakeOut(1);
                    return true;
                } else
                {
                    capi.TriggerIngameError(capi.World.Player, "toomuchborax", "This doesn't need more borax");
                    return false;
                }
            }

            
            for (int i = 0; i < 4; i++)
            {
                if (!inv[i].Empty) continue;
            }

            return true;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();

            foreach (MeshRef meshref in FluxMeshRef)
                meshref?.Dispose();
            
            foreach (MeshRef meshref in BigBellPartMeshRef)
                meshref?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();

            foreach (MeshRef meshref in FluxMeshRef)
                meshref?.Dispose();

            foreach (MeshRef meshref in BigBellPartMeshRef)
                meshref?.Dispose();
        }
    }
}
