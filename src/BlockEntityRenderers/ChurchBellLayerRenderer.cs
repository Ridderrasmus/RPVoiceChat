using RPVoiceChat.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.BlockEntityRenderers
{
    public class ChurchBellLayerRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockEntityChurchBellLayer blockEntityChurchBellLayer;
        public Matrixf ModelMat = new Matrixf();

        public ChurchBellLayerRenderer(ICoreClientAPI capi, BlockEntityChurchBellLayer blockEntityChurchBellPart)
        {
            this.capi = capi;
            this.blockEntityChurchBellLayer = blockEntityChurchBellPart;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
        }

        public double RenderOrder => 0.5;

        public int RenderRange => 25;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (blockEntityChurchBellLayer.BellLayerMeshRef[0] == null) return;

            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            int temp = (int)blockEntityChurchBellLayer.BellLayerSlots[0].Itemstack.Collectible.GetTemperature(capi.World, blockEntityChurchBellLayer.BellLayerSlots[0].Itemstack);
            Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(blockEntityChurchBellLayer.Pos.X, blockEntityChurchBellLayer.Pos.Y, blockEntityChurchBellLayer.Pos.Z);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
            int extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(blockEntityChurchBellLayer.Pos.X, blockEntityChurchBellLayer.Pos.Y, blockEntityChurchBellLayer.Pos.Z);
            prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(blockEntityChurchBellLayer.Pos.X - camPos.X, blockEntityChurchBellLayer.Pos.Y - camPos.Y, blockEntityChurchBellLayer.Pos.Z - camPos.Z)
                .Values;

            prog.RgbaLightIn = lightrgbs;
            prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);
            prog.ExtraGlow = extraGlow;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            

            for (int i = 0; i < blockEntityChurchBellLayer.BellLayerMeshRef.Length; i++)
            {
                if (blockEntityChurchBellLayer.BellLayerMeshRef[i] == null || blockEntityChurchBellLayer.BellLayerMeshRef[i].Disposed) break;
                

                rpi.RenderMesh(blockEntityChurchBellLayer.BellLayerMeshRef[i]);
            }

            for (int i = 0; i < blockEntityChurchBellLayer.FluxMeshRef.Length; i++)
            {
                if (blockEntityChurchBellLayer.FluxMeshRef[i] == null || blockEntityChurchBellLayer.FluxMeshRef[i].Disposed) break;
                
                prog.ExtraGlow = 0;
                rpi.RenderMesh(blockEntityChurchBellLayer.FluxMeshRef[i]);
            }

            prog.Stop();

        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }
    }
}