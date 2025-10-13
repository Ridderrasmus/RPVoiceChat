using RPVoiceChat;
using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.Utils;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

public abstract class BEWeldable : BlockEntityContainer
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
    protected string ResultingBlockCode;

    protected ItemSlot FluxSlot => Inv[0];

    public BEWeldable(int numParts)
    {
        Inv = new InventoryGeneric(numParts + 1, null, null);
        FluxMeshRefs = new MeshRef[numParts];
        PartMeshRefs = new MeshRef[numParts];

        Inv.SlotModified += (i) => MarkDirty(true);
    }

    public override InventoryBase Inventory => Inv;

    public override string InventoryClassName => throw new Exception("BEWeldable is meant to be inherited but wasn't!");

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (api is ICoreClientAPI capi)
        {
            Renderer = new WeldableRenderer(capi, this);

            capi.TesselatorManager.GetDefaultBlockMesh(Block)?.Clear();

            UpdateMeshRefs();
        }
    }

    public virtual bool TestReadyToMerge()
    {
        for (int i = 1; i < Inv.Count; i++)
        {
            ItemSlot slot = Inv[i];
            if (slot.Empty) return false;
            if (slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack) < WeldingMinTemp)
                return false;
        }

        if (FluxSlot.Empty || FluxSlot.Itemstack.StackSize < FluxNeeded)
            return false;

        return true;
    }

    public virtual void ShowReadyToMergeErrors()
    {
        if (Api.Side != EnumAppSide.Client) return;

        var capi = Api as ICoreClientAPI;

        for (int i = 1; i < Inv.Count; i++)
        {
            ItemSlot slot = Inv[i];
            if (slot.Empty)
            {
                capi.TriggerIngameError(capi.World.Player, "missingparts", UIUtils.I18n($"{i18nPrefix}.MissingParts"));
                return;
            }

            if (slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack) < WeldingMinTemp)
            {
                capi.TriggerIngameError(capi.World.Player, "toocold", UIUtils.I18n($"{i18nPrefix}.TooCold"));
                return;
            }
        }

        if (FluxSlot.Empty || FluxSlot.Itemstack.StackSize < FluxNeeded)
        {
            capi.TriggerIngameError(capi.World.Player, "missingflux", UIUtils.I18n($"{i18nPrefix}.MissingFlux"));
        }
    }

    protected virtual void UpdateMeshRefs()
    {
        var capi = Api as ICoreClientAPI;
        if (Api.Side == EnumAppSide.Server || capi == null) return;

        // If the inventory contains flux, then we want to render the flux mesh for as many times flux there is
        // unless there is more flux than there are church bell parts
        for (int i = 0; i < FluxSlot.StackSize; i++)
        {
            if (FluxSlot.Empty) break;

            MeshData meshdata = RenderFlux(i);
            FluxMeshRefs[i] = capi.Render.UploadMesh(meshdata);
        }

        // If the inventory contains the church bell parts, then we want to render the big bell part mesh
        for (int i = 0; i < PartMeshRefs.Length; i++)
        {
            if (Inv[i + 1].Empty) continue;

            MeshData meshdata = RenderPart(i);
            PartMeshRefs[i] = capi.Render.UploadMesh(meshdata);
        }
    }

    protected abstract MeshData RenderPart(int numPart);
    protected abstract MeshData RenderFlux(int numFlux);

    public virtual void OnHammerHitOver(IPlayer byPlayer, Vec3d hitPosition)
    {
        if (!TestReadyToMerge())
        {
            ShowReadyToMergeErrors();
            return;
        }

        HammerHits++;

        ItemSlot slot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
        slot?.Itemstack?.Collectible?.DamageItem(Api.World, byPlayer.Entity, slot);

        float temp = 1500f;
        for (int i = 1; i < Inv.Count; i++)
        {
            ItemSlot partSlot = Inv[i];
            if (partSlot.Empty)
            {
                return;
            }
            float partTemp = partSlot.Itemstack.Collectible.GetTemperature(Api.World, partSlot.Itemstack);
            temp = Math.Min(temp, partTemp);
        }

        if (temp > 800 && Api.Side == EnumAppSide.Client)
        {
            // TODO : set a better position for particle effects because those are currently spawning inside the bell
            // (because the fake block is small as the anvil)
            BlockEntityAnvil.bigMetalSparks.MinPos = Pos.ToVec3d().Add(hitPosition.X, hitPosition.Y, hitPosition.Z);
            BlockEntityAnvil.bigMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 700) / 2, 32, 128);
            Api.World.SpawnParticles(BlockEntityAnvil.bigMetalSparks, byPlayer);

            BlockEntityAnvil.smallMetalSparks.MinPos = Pos.ToVec3d().Add(hitPosition.X, hitPosition.Y, hitPosition.Z);
            BlockEntityAnvil.smallMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 770) / 3, 32, 128);
            Api.World.SpawnParticles(BlockEntityAnvil.smallMetalSparks, byPlayer);
        }

        if (HammerHits >= HammerHitsNeeded && Api.Side == EnumAppSide.Server)
        {
            Block resultingBlock = Api.World.GetBlock(new AssetLocation(RPVoiceChatMod.modID, ResultingBlockCode));

            ItemStack resultingStack = new ItemStack(resultingBlock);
            resultingStack.Collectible.SetTemperature(Api.World, resultingStack, temp);

            var accessor = Api.World.GetBlockAccessor(synchronize: true, relight: true, strict: true, debug: false);
            accessor.SetBlock(resultingBlock.Id, Pos, resultingStack);
        }
    }




    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        HammerHits = tree.GetInt("hammerHits", 0);

        if (Api != null)
            UpdateMeshRefs();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt("hammerHits", HammerHits);
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

        if (TestReadyToMerge())
        {
            dsc.AppendLine(UIUtils.I18n($"{i18nPrefix}.WeldReady"));
        }
        else
        {
            dsc.AppendLine(UIUtils.I18n($"{i18nPrefix}.WeldNotReady"));
        }
    }

    public override void MarkDirty(bool redrawOnClient = false, IPlayer skipPlayer = null)
    {
        base.MarkDirty(redrawOnClient, skipPlayer);

        if (Api.Side == EnumAppSide.Client)
        {
            UpdateMeshRefs();
        }
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();

        Renderer?.Dispose();

        foreach (MeshRef meshRef in FluxMeshRefs)
        {
            meshRef?.Dispose();
        }

        foreach (MeshRef meshRef in PartMeshRefs)
        {
            meshRef?.Dispose();
        }
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();

        Renderer?.Dispose();

        foreach (MeshRef meshRef in FluxMeshRefs)
        {
            meshRef?.Dispose();
        }

        foreach (MeshRef meshRef in PartMeshRefs)
        {
            meshRef?.Dispose();
        }
    }

}
