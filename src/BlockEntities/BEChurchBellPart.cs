﻿using RPVoiceChat.GameContent.BlockEntityRenderers;
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
    public class BlockEntityChurchBellPart : BEWeldable
    {
        public MeshRef[] BellPartMeshRef = new MeshRef[4];
        public MeshRef[] FluxMeshRef = new MeshRef[4];

        // The slot for the flux is the first slot in the inventory
        public ItemSlot FluxSlot => Inv[0];

        // The slots for the big bell parts are the second, third, fourth, and fifth slots in the inventory
        public ItemSlot[] BellPartSlots => new ItemSlot[] { Inv[1], Inv[2], Inv[3], Inv[4] };


        ChurchBellPartRenderer renderer;

        Shape FluxShape;

        public SimpleParticleProperties particleProperties;

        public override InventoryBase Inventory => Inv;

        public override string InventoryClassName => "churchbellpart";

        public BlockEntityChurchBellPart()
        {
            Inv = new InventoryGeneric(5, null, null);

            NeededFlux = 4;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI capi)
            {
                renderer = new ChurchBellPartRenderer(capi, this);


                var assetLoc = new AssetLocation("rpvoicechat", $"shapes/{Block.Shape.Base.Path}-flux.json");
                FluxShape = Shape.TryGet(api, assetLoc);

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
                if (BellPartSlots[i].Empty) break;

                MeshData meshdata;
                capi.Tesselator.TesselateShape(Block, FluxShape, out meshdata);
                meshdata = meshdata.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0, 1.57079633f * (i + 1), 0f);
                FluxMeshRef[i] = capi.Render.UploadMesh(meshdata);
            }

            // If the inventory contains the big bell parts, then we want to render the big bell part mesh
            for (int i = 0; i < BellPartSlots.Length; i++)
            {
                if (BellPartSlots[i].Empty) break;

                MeshData meshdata = capi.TesselatorManager.GetDefaultBlockMesh(Block);
                meshdata = meshdata.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0, 1.57079633f * (i), 0f);
                BellPartMeshRef[i] = capi.Render.UploadMesh(meshdata);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                BellPartSlots[0].Itemstack = byItemStack.Clone();
                BellPartSlots[0].Itemstack.StackSize = 1;

                updateMeshRefs();
            }
        }

        public override void OnHammerHitOver(IPlayer byPlayer, Vec3d hitPosition)
        {

            foreach (ItemSlot slot in BellPartSlots)
                if (slot.Empty) return;

            if (!TestReadyToMerge(false)) return;

            HammerHits++;

            float temp = 1500;
            foreach (ItemSlot slot in BellPartSlots)
                temp = Math.Min(temp, slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack));



            if (temp > 800)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    BlockEntityAnvil.bigMetalSparks.MinPos = Pos.ToVec3d().Add(hitPosition.X, hitPosition.Y, hitPosition.Z);
                    BlockEntityAnvil.bigMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 700) / 2, 32, 128);
                    Api.World.SpawnParticles(BlockEntityAnvil.bigMetalSparks, null);

                    BlockEntityAnvil.smallMetalSparks.MinPos = Pos.ToVec3d().Add(hitPosition.X, hitPosition.Y, hitPosition.Z);
                    BlockEntityAnvil.smallMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 770) / 3, 32, 128);
                    Api.World.SpawnParticles(BlockEntityAnvil.smallMetalSparks, null);
                }
            }

            if (HammerHits > 11)
            {
                var newBlock = Api.World.GetBlock(new AssetLocation(Block.Code.ToString().Replace("part", "layer")));
                ItemStack newStack = new ItemStack(newBlock);
                newStack.Collectible.SetTemperature(Api.World, newStack, temp, false);

                Api.World.BlockAccessor.SetBlock(0, Pos, new ItemStack());

                Inv.Clear();

                Api.World.BlockAccessor.SetBlock(newBlock.Id, Pos, newStack);
            }
        }

        public override bool TestReadyToMerge(bool triggerMessage = true)
        {

            foreach (ItemSlot slot in BellPartSlots)
            { 
                if (slot.Empty)
                {
                    if (triggerMessage && Api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError(capi.World.Player, "missingparts", UIUtils.I18n($"{i18nPrefix}.MissingParts")); //"You need to add all the parts to the weld"
                    }
                    return false;
                }

                if (slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack) < 550)
                {
                    if (triggerMessage && Api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError(capi.World.Player, "toocold", UIUtils.I18n($"{i18nPrefix}.TooCold")); //"Some of the parts are too cold to weld"
                    }
                    return false;
                }
            }

            if (FluxSlot.Empty || FluxSlot.Itemstack.StackSize < NeededFlux)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(capi.World.Player, "missingflux", UIUtils.I18n($"{i18nPrefix}.MissingFlux")); //"You need to add enough powdered borax to the weld as flux"
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
                if (FluxSlot.Empty || FluxSlot.Itemstack.StackSize < 4)
                {
                    

                    ItemStack itemStack = hotbarslot.TakeOut(1);
                    if (FluxSlot.Empty)
                        FluxSlot.Itemstack = itemStack;
                    else
                        FluxSlot.Itemstack.StackSize++;
                    updateMeshRefs();
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
                    if (!BellPartSlots[i].Empty) continue;

                    BellPartSlots[i].Itemstack = hotbarslot.TakeOut(1);
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
            
            foreach (MeshRef meshref in BellPartMeshRef)
                meshref?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();

            foreach (MeshRef meshref in FluxMeshRef)
                meshref?.Dispose();

            foreach (MeshRef meshref in BellPartMeshRef)
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
            foreach (ItemSlot slot in BellPartSlots)
            {
                if (slot.Empty) continue;
                string temp = slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack).ToString();

                dsc.AppendLine($"{slot.Itemstack.GetName()} - {temp}°C");
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
