using RPVoiceChat.BlockEntities;
using RPVoiceChat.Utils;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.BlockEntityRenderers
{
    public class ChurchBellPartRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockEntityChurchBellPart blockEntityChurchBellPart;
        public Matrixf ModelMat = new Matrixf();

        public ChurchBellPartRenderer(ICoreClientAPI capi, BlockEntityChurchBellPart blockEntityChurchBellPart)
        {
            this.capi = capi;
            this.blockEntityChurchBellPart = blockEntityChurchBellPart;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
        }

        public double RenderOrder => 0.5;

        public int RenderRange => 25;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (blockEntityChurchBellPart.BellPartMeshRef[0] == null) return;

            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            int temp = (int)blockEntityChurchBellPart.BellPartSlots[0].Itemstack.Collectible.GetTemperature(capi.World, blockEntityChurchBellPart.BellPartSlots[0].Itemstack);
            Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(blockEntityChurchBellPart.Pos.X, blockEntityChurchBellPart.Pos.Y, blockEntityChurchBellPart.Pos.Z);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
            int extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(blockEntityChurchBellPart.Pos.X, blockEntityChurchBellPart.Pos.Y, blockEntityChurchBellPart.Pos.Z);
            prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(blockEntityChurchBellPart.Pos.X - camPos.X, blockEntityChurchBellPart.Pos.Y - camPos.Y, blockEntityChurchBellPart.Pos.Z - camPos.Z)
                .Values;

            prog.RgbaLightIn = lightrgbs;
            prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);
            prog.ExtraGlow = extraGlow;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            

            for (int i = 0; i < blockEntityChurchBellPart.BellPartMeshRef.Length; i++)
            {
                if (blockEntityChurchBellPart.BellPartMeshRef[i] == null || blockEntityChurchBellPart.BellPartMeshRef[i].Disposed) break;
                

                rpi.RenderMesh(blockEntityChurchBellPart.BellPartMeshRef[i]);
            }

            for (int i = 0; i < blockEntityChurchBellPart.FluxMeshRef.Length; i++)
            {
                if (blockEntityChurchBellPart.FluxMeshRef[i] == null || blockEntityChurchBellPart.FluxMeshRef[i].Disposed) break;
                
                prog.ExtraGlow = 0;
                rpi.RenderMesh(blockEntityChurchBellPart.FluxMeshRef[i]);
            }

            prog.Stop();

        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }
    }
}