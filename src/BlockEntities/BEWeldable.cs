using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.Utils;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntities
{
    public class BEWeldable : BlockEntityContainer
    {
        protected string i18nPrefix = "Welding";
        protected int WeldingMinTemp = 550;
        protected int HammerHitsNeeded = 11;
        protected int HammerHits = 0;
        protected int FluxNeeded = 0;
        protected WeldableRenderer Renderer;
        public InventoryGeneric Inv;
        public MeshRef[] FluxMeshRefs;
        public MeshRef[] PartMeshRefs;

        protected ItemSlot FluxSlot => Inv[0];

        public BEWeldable(int numParts)
        {
            Inv = new InventoryGeneric(numParts + 1, null, null);
            Renderer = new WeldableRenderer(Api as ICoreClientAPI, this);
            FluxMeshRefs = new MeshRef[numParts];
            PartMeshRefs = new MeshRef[numParts];
        }

        public override InventoryBase Inventory => Inv;

        public override string InventoryClassName => throw new Exception("BEWeldable is meant to be inherited but wasn't!");

        public virtual bool TestReadyToMerge(bool triggerMessage = true)
        {
            for (int i = 1; i < Inv.Count; i++)
            {
                ItemSlot slot = Inv[i];
                if (slot.Empty)
                {
                    if (triggerMessage && Api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError(capi.World.Player, "missingparts", UIUtils.I18n($"{i18nPrefix}.MissingParts")); //"You need to add all the parts to the weld"
                    }
                    return false;
                }

                if (slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack) < WeldingMinTemp)
                {
                    if (triggerMessage && Api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError(capi.World.Player, "toocold", UIUtils.I18n($"{i18nPrefix}.TooCold")); //"Some of the parts are too cold to weld"
                    }
                    return false;
                }
            }

            if (FluxSlot.Empty || FluxSlot.Itemstack.StackSize < FluxNeeded)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(capi.World.Player, "missingflux", UIUtils.I18n($"{i18nPrefix}.MissingFlux")); //"You need to add enough powdered borax to the weld as flux"
                }
                return false;
            }

            return true;
        }

        protected virtual void UpdateMeshRefs()
        {
            if (Api.Side == EnumAppSide.Server) return;

            var capi = Api as ICoreClientAPI;

            // If the inventory contains flux, then we want to render the flux mesh for as many times flux there is
            // unless there is more flux than there are church bell parts
            for (int i = 0; i < FluxSlot.StackSize; i++)
            {
                if (Inv[0].Empty) break;

                MeshData meshdata = RenderFlux(i);

                FluxMeshRefs[i] = capi.Render.UploadMesh(meshdata);
            }

            // If the inventory contains the big bell parts, then we want to render the big bell part mesh
            for (int i = 0; i < PartMeshRefs.Length; i++)
            {
                if (Inv[i + 1].Empty) continue;

                MeshData meshdata = RenderPart(i);

                PartMeshRefs[i] = capi.Render.UploadMesh(meshdata);
            }
        }

        protected virtual MeshData RenderPart(int numPart)
        {
            throw new Exception("BEWeldable is meant to be inherited but wasn't!");
        }

        protected virtual MeshData RenderFlux(int numFlux)
        {
            throw new Exception("BEWeldable is meant to be inherited but wasn't!");
        }

        public virtual void OnHammerHitOver(IPlayer byPlayer, Vec3d hitPosition)
        {
            throw new Exception("BEWeldable is meant to be inherited but wasn't!");
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
            for (int i = 1; i < Inv.Count; i++)
            {
                ItemSlot slot = Inv[i];
                if (slot.Empty) continue;

                dsc.AppendLine($"{slot.Itemstack.GetName()} - {slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack)}°C");
            }

            if (!FluxSlot.Empty)
            {
                dsc.AppendLine($"{FluxSlot.Itemstack.StackSize} {FluxSlot.Itemstack.GetName()}");
            }

            if (TestReadyToMerge(false))
            {
                dsc.AppendLine(UIUtils.I18n($"{i18nPrefix}.WeldReady")); //"Ready to weld"
            }
            else
            {
                dsc.AppendLine(UIUtils.I18n($"{i18nPrefix}.WeldNotReady")); //"Not ready to weld"
            }
        }


    }
}
