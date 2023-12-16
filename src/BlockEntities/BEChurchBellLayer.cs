using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.Utils;
using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntities
{
    public class BlockEntityChurchBellLayer : BEWeldable
    {

        // If you're looking through this, I know, it's a mess ain't it?
        // This is my attempt at making something weldable.


        private string[] bellLayerNames = new string[] { "churchbell-layer-bottom", "churchbell-layer-middle", "churchbell-layer-top", "churchbell-layer-topmost" };
        private float[] bellLayerHeights = new float[] { 0f, 0.750f, 1.25f, 1.75f };
        


        // The slots for the big bell parts are the second, third, fourth, and fifth slots in the inventory
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

            if (api is ICoreClientAPI capi)
            {
                Renderer = new WeldableRenderer(capi, this);


                UpdateMeshRefs();
            }
        }

        protected override void UpdateMeshRefs()
        {
            if (Api.Side == EnumAppSide.Server) return;

            ICoreClientAPI capi = Api as ICoreClientAPI;

            // If the inventory contains the big bell parts, then we want to render the big bell part mesh
            for (int i = 0; i < BellLayerSlots.Length; i++)
            {
                if (BellLayerSlots[i].Empty) continue;

                MeshData meshdata = RenderPart(i);

                PartMeshRefs[i] = capi.Render.UploadMesh(meshdata);
            }

            // If the inventory contains flux AND two big bell layers then we want to render the flux mesh between the two big bell layers
            // The fitting flux mesh is derived from the lower big bell layer
            for (int i = 0; i < FluxNeeded; i++)
            {
                if (BellLayerSlots[i].Empty && BellLayerSlots[i+1].Empty) break;

                if (FluxSlot.Empty || FluxSlot.StackSize < i+1) break;

                MeshData meshdata = RenderFlux(i);
                FluxMeshRefs[i] = capi.Render.UploadMesh(meshdata);
            }

        }

        protected override MeshData RenderPart(int numPart)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;

            MeshData meshdata = capi.TesselatorManager.GetDefaultBlockMesh(BellLayerSlots[numPart].Itemstack.Block).Clone();

            for (int j = 0; j <= numPart; j++)
            {
                if (!BellLayerSlots[j].Empty)
                    meshdata = meshdata.Translate(0, bellLayerHeights[j], 0);
            }
            return meshdata;
        }

        protected override MeshData RenderFlux(int numFlux)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;

            var fluxShape = Shape.TryGet(Api, new AssetLocation("rpvoicechat", $"shapes/block/churchbell/{bellLayerNames[numFlux]}-flux.json"));
            if (fluxShape == null)
                throw new Exception($"Layer flux shape is null for: {bellLayerNames[numFlux]}");

            capi.Tesselator.TesselateShape(Block, fluxShape, out MeshData meshdata);

            for (int j = 0; j < numFlux; j++)
                if (!BellLayerSlots[j].Empty)
                {
                    meshdata = meshdata.Translate(0, bellLayerHeights[j+1], 0);
                }
            return meshdata;
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

                UpdateMeshRefs();
            }
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
                    UpdateMeshRefs();
                    return true;
                } else if (!FluxSlot.Empty && FluxSlot.Itemstack.StackSize < filledLayerSlots - 1)
                {
                    FluxSlot.Itemstack.StackSize++;
                    hotbarslot.TakeOut(1);
                    UpdateMeshRefs();
                    return true;
                } else
                {
                    capi?.TriggerIngameError(capi.World.Player, "toomuchflux", UIUtils.I18n($"{i18nPrefix}.TooMuchFlux")); //"This doesn't need more borax"
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
                    UpdateMeshRefs();
                    return true;
                }
            }

            return true;
        }
    }
}
