using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.GameContent.BlockEntities
{
    public class BlockEntityChurchBellPart : BEWeldable
    {

        // If you're looking through this, I know, it's a mess ain't it?
        // This is my attempt at making something weldable.

        Shape FluxShape;


        public override string InventoryClassName => "churchbellpart";

        public BlockEntityChurchBellPart() : base(4)
        {
            FluxNeeded = 4;
            
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ResultingBlockCode = Block.Code.Path.Replace("part", "layer");

            if (api is ICoreClientAPI capi)
            {
                Renderer = new WeldableRenderer(capi, this);


                var assetLoc = new AssetLocation("rpvoicechat", $"shapes/{Block.Shape.Base.Path}-flux.json");
                FluxShape = Shape.TryGet(api, assetLoc);

                UpdateMeshRefs();
            }
        }

        protected override MeshData RenderPart(int numPart)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            Shape shape = Shape.TryGet(Api, new AssetLocation("rpvoicechat", $"shapes/{Block.Shape.Base.Path}.json"));
            MeshData meshdata = new MeshData();
            capi.Tesselator.TesselateShape(Block, shape, out meshdata);
            return meshdata.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0, 1.57079633f * (numPart), 0f);

        }

        protected override MeshData RenderFlux(int numFlux)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            capi.Tesselator.TesselateShape(Block, FluxShape, out MeshData meshdata);
            return meshdata.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0, 1.57079633f * (numFlux), 0f);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                Inv[1].Itemstack = byItemStack.Clone();
                Inv[1].Itemstack.StackSize = 1;

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
                if (FluxSlot.Empty || FluxSlot.Itemstack.StackSize < 4)
                {
                    

                    ItemStack itemStack = hotbarslot.TakeOut(1);
                    if (FluxSlot.Empty)
                        FluxSlot.Itemstack = itemStack;
                    else
                        FluxSlot.Itemstack.StackSize++;
                    UpdateMeshRefs();
                    return true;
                } 
                else
                {
                    capi?.TriggerIngameError(capi.World.Player, "toomuchflux", UIUtils.I18n($"{i18nPrefix}.TooMuchFlux")); //"This doesn't need more borax"
                    return false;
                }
            }

            if (hotbarslot.Itemstack.Collectible.Code.Path == Block.Code.Path)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (!Inv[i+1].Empty) continue;

                    Inv[i + 1].Itemstack = hotbarslot.TakeOut(1);
                    UpdateMeshRefs();
                    return true;
                }


            }

            return true;
        }
    }
}
