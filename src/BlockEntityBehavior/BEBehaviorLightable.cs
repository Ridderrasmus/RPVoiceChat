using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

/// <summary>
/// BlockEntityBehavior for blocks that can emit light (lightable).
/// Implements IPointLight directly to serve as the light source.
/// </summary>
public class BEBehaviorLightable : BlockEntityBehavior, IPointLight
{
    private bool isLightActive = false;
    private Vec3f lightColor = new Vec3f(1.0f, 0.9f, 0.7f); // Warm color by default
    private float lightLevel = 1.0f;
    private float lightRadius = 10.0f;
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
        if (isLightActive == active) return;

        isLightActive = active;
        Blockentity.MarkDirty();

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

    /// <summary>
    /// Sets the light radius
    /// </summary>
    public void SetLightRadius(float radius)
    {
        lightRadius = Math.Max(0.1f, radius);
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

        if (api.Side == EnumAppSide.Client)
        {
            capi = api as ICoreClientAPI;
        }
    }

    private void UpdateLight()
    {
        if (capi == null) return;

        // Always remove the light first (in case it was already added)
        capi.Render.RemovePointLight(this);

        // Add the light only if it should be active
        if (isLightActive)
        {
            // Position the light at the center of the block
            Vec3d lightPos = new Vec3d(Blockentity.Pos.X + 0.5, Blockentity.Pos.Y + 0.5, Blockentity.Pos.Z + 0.5);

            // Update IPointLight properties
            Pos = lightPos;
            Color = new Vec3f(lightColor.X * lightLevel, lightColor.Y * lightLevel, lightColor.Z * lightLevel);

            capi.Render.AddPointLight(this);
        }
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
        // Always remove the light if the API is available, regardless of isLightActive state
        // This ensures cleanup when the block is removed or unloaded
        if (capi != null)
        {
            capi.Render.RemovePointLight(this);
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
        lightRadius = tree.GetFloat("lightRadius", 10.0f);

        if (Blockentity.Api?.Side == EnumAppSide.Client)
        {
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
        tree.SetFloat("lightRadius", lightRadius);
    }
}
