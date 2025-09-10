using RPVoiceChat.GameContent.BlockEntities;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Renderers
{
    public class WeldableRenderer : IRenderer
    {
        protected BEWeldable BEWeldable;
        
        protected ICoreClientAPI capi;

        public double RenderOrder => 0.5;
        public int RenderRange => 25;
        
        public Matrixf ModelMat = new Matrixf();

        public WeldableRenderer(ICoreClientAPI capi, BEWeldable BEWeldable)
        {
            if (capi == null) return;
            this.capi = capi;
            this.BEWeldable = BEWeldable;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
        }

        public virtual void Dispose()
        {
            if (capi == null) return;
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        public virtual void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            // If any all PartMeshRefs are null, then we don't want to render anything
            if (BEWeldable.PartMeshRefs.All(x => x == null)) return;

            // Get index of first variable in list PartMeshRefs that isn't null
            int firstNonNull = Array.FindIndex(BEWeldable.PartMeshRefs, x => x != null);

            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            
            int temp = (int)BEWeldable.Inv[firstNonNull + 1].Itemstack.Collectible.GetTemperature(capi.World, BEWeldable.Inv[firstNonNull + 1].Itemstack);
            Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(BEWeldable.Pos.X, BEWeldable.Pos.Y, BEWeldable.Pos.Z);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
            int extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(BEWeldable.Pos.X, BEWeldable.Pos.Y, BEWeldable.Pos.Z);
            prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(BEWeldable.Pos.X - camPos.X, BEWeldable.Pos.Y - camPos.Y, BEWeldable.Pos.Z - camPos.Z)
                .Values;

            prog.RgbaLightIn = lightrgbs;
            prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);
            prog.ExtraGlow = extraGlow;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;



            for (int i = 0; i < BEWeldable.PartMeshRefs.Length; i++)
            {
                if (BEWeldable.PartMeshRefs[i] == null || BEWeldable.PartMeshRefs[i].Disposed) continue;

                temp = (int)BEWeldable.Inv[i+1].Itemstack.Collectible.GetTemperature(capi.World, BEWeldable.Inv[i+1].Itemstack);
                lightrgbs = capi.World.BlockAccessor.GetLightRGBs(BEWeldable.Pos.X, BEWeldable.Pos.Y, BEWeldable.Pos.Z);
                glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
                extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);
                prog.RgbaLightIn = lightrgbs;
                prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);
                prog.ExtraGlow = extraGlow;

                rpi.RenderMesh(BEWeldable.PartMeshRefs[i]);
            }

            for (int i = 0; i < BEWeldable.FluxMeshRefs.Length; i++)
            {
                if (BEWeldable.FluxMeshRefs[i] == null || BEWeldable.FluxMeshRefs[i].Disposed) continue;

                prog.ExtraGlow = 0;
                rpi.RenderMesh(BEWeldable.FluxMeshRefs[i]);
            }

            prog.Stop();
        }
    }
}
