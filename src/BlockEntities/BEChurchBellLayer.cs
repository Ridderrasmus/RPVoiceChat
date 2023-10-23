using RPVoiceChat.BlockEntityRenderers;
using RPVoiceChat.Utils;
using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.BlockEntities
{
    public class BlockEntityChurchBellLayer : BlockEntityContainer
    {
        private string[] bellLayerNames = new string[] { "churchbell-layer-bottom", "churchbell-layer-middle", "churchbell-layer-top", "churchbell-layer-topmost" };


        public MeshRef[] BellLayerMeshRef = new MeshRef[4];
        public MeshRef[] FluxMeshRef = new MeshRef[4];

        // The inveotry slot for the church bell parts
        InventoryGeneric inv;

        // The slot for the flux is the first slot in the inventory
        public ItemSlot FluxSlot => inv[0];

        // The slots for the big bell parts are the second, third, fourth, and fifth slots in the inventory
        public ItemSlot[] BellLayerSlots => new ItemSlot[] { inv[1], inv[2], inv[3], inv[4] };


        // Stuff that should be defined in JSON
        public int ron;
        public int FluxShapePath;
        public int hammerHits;


        ChurchBellLayerRenderer renderer;

        MeshData defMesh;

        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "churchbelllayer";

        public string[] BellLayerName { get => bellLayerNames; set => bellLayerNames = value; }

        public BlockEntityChurchBellLayer()
        {
            inv = new InventoryGeneric(5, null, null);


        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI capi)
            {
                renderer = new ChurchBellLayerRenderer(capi, this);

                defMesh = capi.TesselatorManager.GetDefaultBlockMesh(Block).Clone();

                updateMeshRefs();
            }
        }

        void updateMeshRefs()
        {
            if (Api.Side == EnumAppSide.Server) return;
            
            var capi = Api as ICoreClientAPI;


            // If the inventory contains flux, then we want to render the flux mesh for as many times flux there is
            // unless there is more flux than there are church bell parts
            for (int i = 0; i < FluxSlot.StackSize; i++)
            {
                if (BellLayerSlots[i].Empty) break;

                var fluxPart = Shape.TryGet(Api, new AssetLocation("rpvoicechat", "shapes/block/churchbell/botpartflux.json"));
                if (fluxPart == null)
                    throw new Exception("Flux shape is null");

                MeshData meshdata;
                capi.Tesselator.TesselateShape(Block, fluxPart, out meshdata);
                meshdata = meshdata.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0, 1.57079633f * (i + 1), 0f);
                FluxMeshRef[i] = capi.Render.UploadMesh(meshdata);
            }

            // If the inventory contains the big bell parts, then we want to render the big bell part mesh
            for (int i = 0; i < BellLayerSlots.Length; i++)
            {
                if (BellLayerSlots[i].Empty) break;

                MeshData meshdata = defMesh;
                meshdata = meshdata.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0, 1.57079633f * (i), 0f);
                BellLayerMeshRef[i] = capi.Render.UploadMesh(meshdata);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (byItemStack.Collectible.Code.Path == BellLayerName[i])
                    {
                        BellLayerSlots[i].Itemstack = byItemStack.Clone();
                        BellLayerSlots[i].Itemstack.StackSize = 1;
                        break;
                    }
                }

                updateMeshRefs();
            }
        }

        public void OnHammerHitOver(IPlayer byPlayer, Vec3d hitPosition)
        {
            foreach (ItemSlot slot in BellLayerSlots)
                if (slot.Empty) return;

            if (!TestReadyToMerge(false)) return;

            hammerHits++;

            float temp = 1500;
            foreach (ItemSlot slot in BellLayerSlots)
                temp = Math.Min(temp, slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack));
            

            if (temp > 800)
            {
                BlockEntityAnvil.bigMetalSparks.MinPos = Pos.ToVec3d().Add(hitPosition.X, hitPosition.Y, hitPosition.Z);
                BlockEntityAnvil.bigMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 700) / 2, 32, 128);
                Api.World.SpawnParticles(BlockEntityAnvil.bigMetalSparks, byPlayer);

                BlockEntityAnvil.smallMetalSparks.MinPos = Pos.ToVec3d().Add(hitPosition.X, hitPosition.Y, hitPosition.Z);
                BlockEntityAnvil.smallMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 770) / 3, 32, 128);
                Api.World.SpawnParticles(BlockEntityAnvil.smallMetalSparks, byPlayer);
            }

            if (hammerHits > 11)
            {
                Api.World.BlockAccessor.SetBlock(Api.World.GetBlock(new AssetLocation("churchbell")).Id, Pos);
            }
        }

        private bool TestReadyToMerge(bool triggerMessage = true)
        {

            foreach (ItemSlot slot in BellLayerSlots)
            { 
                if (slot.Empty)
                {
                    if (triggerMessage && Api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError(capi.World.Player, "missingparts", "You need to add all the parts to the weld");
                    }
                    return false;
                }

                if (slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack) < 550)
                {
                    if (triggerMessage && Api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError(capi.World.Player, "toocold", "Some of the parts are too cold to weld");
                    }
                    return false;
                }
            }

            if (FluxSlot.Empty)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(capi.World.Player, "missingflux", "You need to add powdered borax to the weld as flux");
                }
                return false;
            }

            return true;
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot hotbarslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ICoreClientAPI capi = Api as ICoreClientAPI;

            if (hotbarslot.Empty) return true;

            if (hotbarslot.Itemstack.Collectible.Code.Path == "powderedborax")
            {

                // Right clicks with powdered borax

                // Check amount of filled Layer slots
                int filledLayerSlots = 0;
                foreach (ItemSlot slot in BellLayerSlots)
                    if (!slot.Empty) filledLayerSlots++;

                if (FluxSlot.Empty && 1 < filledLayerSlots)
                {
                    FluxSlot.Itemstack = hotbarslot.TakeOut(1);
                    updateMeshRefs();
                    return true;
                } else if (FluxSlot.Itemstack.StackSize < filledLayerSlots - 1)
                {
                    FluxSlot.Itemstack.StackSize++;
                    hotbarslot.TakeOut(1);
                    updateMeshRefs();
                    return true;
                } else
                {
                    capi?.TriggerIngameError(capi.World.Player, "toomuchborax", "This doesn't need more borax");
                    return false;
                }
            }



            if (bellLayerNames.Contains(hotbarslot.Itemstack.Collectible.Code.Path))
            {
                for (int i = 0; i < 4; i++)
                {

                    // If current container slot is empty or the item in the hotbar doesn't fit to the current container slot, continue
                    if (!BellLayerSlots[i].Empty || hotbarslot.Itemstack.Collectible.Code.Path != BellLayerName[i]) continue;

                    BellLayerSlots[i].Itemstack = hotbarslot.TakeOut(1);
                    updateMeshRefs();
                    return true;
                }
            }

            return true;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();

            foreach (MeshRef meshref in FluxMeshRef)
                meshref?.Dispose();
            
            foreach (MeshRef meshref in BellLayerMeshRef)
                meshref?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();

            foreach (MeshRef meshref in FluxMeshRef)
                meshref?.Dispose();

            foreach (MeshRef meshref in BellLayerMeshRef)
                meshref?.Dispose();
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            return true;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            foreach (ItemSlot slot in BellLayerSlots)
            {
                if (slot.Empty) continue;
                string temp = slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack).ToString();

                dsc.AppendLine(slot.Itemstack.GetName() + " - " + temp + "°C");
            }

            if (!FluxSlot.Empty)
            {
                dsc.AppendLine(FluxSlot.Itemstack.StackSize + " " + FluxSlot.Itemstack.GetName());
            }

            if (TestReadyToMerge(false))
            {
                dsc.AppendLine("Ready to weld");
            }
            else
            {
                dsc.AppendLine("Not ready to weld");
            }
        }

    }
}
