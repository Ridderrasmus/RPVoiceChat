using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using RPVoiceChat.GameContent;
using RPVoiceChat.Util;

/// <summary>
/// BlockEntityBehavior for blocks that can emit light (lightable).
/// Implements IPointLight. Light origin: from IBlockEntityWithCustomLightPosition.GetLightOrigin() if implemented, else block center.
/// Never does Remove then re-Add of the same instance (avoids render-side invalidation).
/// </summary>
public class BEBehaviorLightable : BlockEntityBehavior, IPointLight
{
    private bool isLightActive = false;
    private bool lightInRenderList; // avoids Remove then re-Add same instance
    private Vec3f lightColor = new Vec3f(1.0f, 0.9f, 0.7f); // Warm color by default
    private float lightLevel = 1.0f;
    /// <summary>
    /// IPointLight has no radius: engine often uses Color magnitude as intensity/ falloff proxy.
    /// Scale &gt; 1 makes the dynamic light much brighter / larger-looking (optional JSON lightIntensityScale).
    /// </summary>
    private float lightIntensityScale = 1.0f;
    private ICoreClientAPI capi;

    // IPointLight implementation
    public new Vec3d Pos { get; private set; }
    public Vec3f Color { get; private set; }

    public BEBehaviorLightable(Vintagestory.API.Common.BlockEntity blockEntity) : base(blockEntity)
    {
    }

    /// <summary>
    /// Activates or deactivates the light
    /// </summary>
    public void SetLightActive(bool active)
    {
        if (isLightActive == active)
        {
            // Recover from load-order edge cases where state is restored before render registration.
            if (active && Blockentity.Api?.Side == EnumAppSide.Client)
                UpdateLight();
            return;
        }

        isLightActive = active;
        Blockentity.MarkDirty(Blockentity.Api?.Side == EnumAppSide.Server);

        if (Blockentity.Api?.Side == EnumAppSide.Client)
        {
            UpdateLight();
        }
    }

    /// <summary>
    /// Sets the light color
    /// </summary>
    public void SetLightColor(Vec3f color)
    {
        lightColor = color;
        if (isLightActive && Blockentity.Api?.Side == EnumAppSide.Client)
        {
            UpdateLight();
        }
        Blockentity.MarkDirty();
    }

    /// <summary>
    /// Sets the light level (0.0 to 1.0)
    /// </summary>
    public void SetLightLevel(float level)
    {
        lightLevel = Math.Max(0.0f, Math.Min(1.0f, level));
        if (isLightActive && Blockentity.Api?.Side == EnumAppSide.Client)
        {
            UpdateLight();
        }
        Blockentity.MarkDirty();
    }

    public bool IsLightActive => isLightActive;

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        if (properties != null)
        {
            if (properties["lightLevel"].Exists)
                lightLevel = properties["lightLevel"].AsFloat(1.0f);
            if (properties["lightColorX"].Exists || properties["lightColorY"].Exists || properties["lightColorZ"].Exists)
                lightColor = new Vec3f(
                    properties["lightColorX"].AsFloat(1.0f),
                    properties["lightColorY"].AsFloat(0.9f),
                    properties["lightColorZ"].AsFloat(0.7f));
            if (properties["lightIntensityScale"].Exists)
                lightIntensityScale = GameMath.Clamp(properties["lightIntensityScale"].AsFloat(1.0f), 0.1f, 25f);
        }

        if (api.Side == EnumAppSide.Client)
        {
            capi = api as ICoreClientAPI;
            // Ensure the light is registered if attributes were loaded before Initialize().
            UpdateLight();
        }
    }

    /// <summary>Origin of the point light: supplied by the block entity (GetLightOrigin) or block center by default.</summary>
    private Vec3d GetLightOrigin() =>
        Blockentity is IBlockEntityWithCustomLightPosition custom
            ? custom.GetLightOrigin()
            : Blockentity.Pos.ToWorldCenter();

    private void UpdateLight()
    {
        if (capi == null) return;

        if (!isLightActive)
        {
            if (lightInRenderList)
            {
                capi.Render.RemovePointLight(this);
                lightInRenderList = false;
            }
            return;
        }
        float s = lightLevel * lightIntensityScale;
        var c = new Vec3f(lightColor.X * s, lightColor.Y * s, lightColor.Z * s);
        if (lightInRenderList)
        {
            Pos = GetLightOrigin();
            Color = c;
            return;
        }
        Pos = GetLightOrigin();
        Color = c;
        capi.Render.AddPointLight(this);
        lightInRenderList = true;
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        RemoveLight();
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        RemoveLight();
    }

    private void RemoveLight()
    {
        if (capi != null && lightInRenderList)
        {
            capi.Render.RemovePointLight(this);
            lightInRenderList = false;
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        
        isLightActive = tree.GetBool("isLightActive", false);
        lightColor = new Vec3f(
            tree.GetFloat("lightColorX", 1.0f),
            tree.GetFloat("lightColorY", 0.9f),
            tree.GetFloat("lightColorZ", 0.7f)
        );
        lightLevel = tree.GetFloat("lightLevel", 1.0f);

        if (Blockentity.Api?.Side == EnumAppSide.Client)
        {
            capi ??= worldAccessForResolve?.Api as ICoreClientAPI;
            UpdateLight();
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        
        tree.SetBool("isLightActive", isLightActive);
        tree.SetFloat("lightColorX", lightColor.X);
        tree.SetFloat("lightColorY", lightColor.Y);
        tree.SetFloat("lightColorZ", lightColor.Z);
        tree.SetFloat("lightLevel", lightLevel);
        tree.SetFloat("lightIntensityScale", lightIntensityScale);
    }
}
