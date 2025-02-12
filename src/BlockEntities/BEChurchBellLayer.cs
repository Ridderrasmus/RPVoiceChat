using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.Utils;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RPVoiceChat.GameContent.BlockEntities
{
    public class BlockEntityChurchBellLayer : BEWeldable
    {

        // If you're looking through this, I know, it's a mess ain't it?
        // This is my attempt at making something weldable.


        private string[] bellLayerNames = new string[] { "churchbell-layer-bottom", "churchbell-layer-middle", "churchbell-layer-top", "churchbell-layer-topmost" };
        private float[] bellLayerHeights = new float[] { 0f, 0.750f, 1.25f, 1.75f };
        


        // The slots for the church bell parts are the second, third, fourth, and fifth slots in the inventory
        public ItemSlot[] BellLayerSlots => new ItemSlot[] { Inv[1], Inv[2], Inv[3], Inv[4] };

        public override string InventoryClassName => "churchbelllayer";

        public string[] BellLayerName { get => bellLayerNames; set => bellLayerNames = value; }

        public BlockEntityChurchBellLayer() : base(4)
        {
            FluxNeeded = 3;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ResultingBlockCode = "churchbell";

            if (api is ICoreClientAPI capi)
            {
                Renderer = new WeldableRenderer(capi, this);



                Inv.SlotModified += (i) =>
                {
                    UpdateMeshRefs();
                };
            }

            UpdateMeshRefs();
        }

        protected override void UpdateMeshRefs()
        {
            if (Api.Side == EnumAppSide.Server) return;
            
            ICoreClientAPI capi = Api as ICoreClientAPI;

            // If the inventory contains the church bell parts, then we want to render the church bell part mesh
            for (int i = 0; i < 4; i++)
            {
                if (BellLayerSlots[i].Empty) continue;

                MeshData meshdata = RenderPart(i);

                PartMeshRefs[i] = capi.Render.UploadMesh(meshdata);
                meshdata.Clear();
            }

            // If the inventory contains flux AND two church bell layers then we want to render the flux mesh between the two church bell layers
            // The fitting flux mesh is derived from the lower church bell layer
            for (int i = 0; i < FluxNeeded; i++)
            {
                if (BellLayerSlots[i].Empty && BellLayerSlots[i+1].Empty) continue;

                if (FluxSlot.Empty || FluxSlot.StackSize < i+1) continue;

                MeshData meshdata = RenderFlux(i);
                FluxMeshRefs[i] = capi.Render.UploadMesh(meshdata);
                meshdata.Clear();
            }

        }

        protected override MeshData RenderPart(int numPart)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;

            Shape shape = Shape.TryGet(Api, new AssetLocation("rpvoicechat", $"shapes/{BellLayerSlots[numPart].Itemstack.Block.Shape.Base.Path}.json"));
            MeshData meshdata = new MeshData();
            capi.Tesselator.TesselateShape(Block, shape, out meshdata);

            float vertOffset = 0;
            for (int j = 0; j < numPart; j++)
                if (!BellLayerSlots[j].Empty)
                    vertOffset += bellLayerHeights[j+1];
                
            return meshdata.Translate(0, vertOffset, 0);
        }

        protected override MeshData RenderFlux(int numFlux)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            var fluxShape = Shape.TryGet(Api, new AssetLocation("rpvoicechat", $"shapes/block/churchbell/{bellLayerNames[numFlux]}-flux.json"));
            if (fluxShape == null)
                throw new Exception($"Layer flux shape is null for: {bellLayerNames[numFlux]}");

            capi.Tesselator.TesselateShape(Block, fluxShape, out MeshData meshdata);

            float vertOffset = 0;
            for (int j = 0; j < numFlux; j++)
                if (!BellLayerSlots[j].Empty)
                    vertOffset += bellLayerHeights[j + 1];
                
            return meshdata.Translate(0, vertOffset, 0);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (Api.Side == EnumAppSide.Client) return;

            if (byItemStack != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    // I know, this is messy but I just want this done
                    string[] split = BellLayerName[i].Split("-");
                    string layerName = $"{split[0]}-{split[1]}-brass-{split[2]}";
                    if (byItemStack.Collectible.Code.Path == layerName)
                    {
                        BellLayerSlots[i].Itemstack = byItemStack.Clone();
                        BellLayerSlots[i].Itemstack.StackSize = 1;
                        MarkDirty(true);
                        break;
                    }
                }

            }
            UpdateMeshRefs();

        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot hotbarslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ICoreClientAPI capi = Api as ICoreClientAPI;

            if (hotbarslot.Empty) return true;

            if (hotbarslot.Itemstack.Collectible.Code.Path == "powder-borax")
            {

                // Right clicks with powdered borax

                // Check amount of filled Layer slots
                int filledLayerSlots = 0;
                foreach (ItemSlot slot in BellLayerSlots)
                    if (!slot.Empty) filledLayerSlots++;

                if (FluxSlot.Empty && 1 < filledLayerSlots)
                {
                    FluxSlot.Itemstack = hotbarslot.TakeOut(1);
                    MarkDirty(true);
                    UpdateMeshRefs();
                    return true;
                } else if (!FluxSlot.Empty && FluxSlot.Itemstack.StackSize < filledLayerSlots - 1)
                {
                    FluxSlot.Itemstack.StackSize++;
                    MarkDirty(true);
                    hotbarslot.TakeOut(1);
                    UpdateMeshRefs();
                    return true;
                } else
                {
                    capi?.TriggerIngameError(capi.World.Player, "toomuchflux", UIUtils.I18n($"{i18nPrefix}.TooMuchFlux")); //"This doesn't need more borax"
                    return false;
                }
            }

            string[] layerNames = new string[] { "churchbell-layer-brass-bottom", "churchbell-layer-brass-middle", "churchbell-layer-brass-top", "churchbell-layer-brass-topmost" };
            if (layerNames.Contains(hotbarslot.Itemstack.Collectible.Code.Path))
            {
                for (int i = 0; i < 4; i++)
                {

                    // If current container slot is empty or the item in the hotbar doesn't fit to the current container slot, continue
                    if (!BellLayerSlots[i].Empty || hotbarslot.Itemstack.Collectible.Code.Path != layerNames[i]) continue;

                    BellLayerSlots[i].Itemstack = hotbarslot.TakeOut(1);
                    MarkDirty(true);
                    UpdateMeshRefs();
                    return true;
                }
            }


            return true;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api != null)
                UpdateMeshRefs();
        }

    }
}
