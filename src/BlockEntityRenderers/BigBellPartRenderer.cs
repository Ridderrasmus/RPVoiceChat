using RPVoiceChat.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.BlockEntityRenderers
{
    public class BigBellPartRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockEntityBigBellPart blockEntityBigBellPart;
        public Matrixf ModelMat = new Matrixf();

        public BigBellPartRenderer(ICoreClientAPI capi, BlockEntityBigBellPart blockEntityBigBellPart)
        {
            this.capi = capi;
            this.blockEntityBigBellPart = blockEntityBigBellPart;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
        }

        public double RenderOrder => 0.5;

        public int RenderRange => 25;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (blockEntityBigBellPart.Inventory[0].Empty) return;

            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            int temp = (int)blockEntityBigBellPart.Inventory[0].Itemstack.Collectible.GetTemperature(capi.World, blockEntityBigBellPart.Inventory[0].Itemstack);
            Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(blockEntityBigBellPart.Pos.X, blockEntityBigBellPart.Pos.Y, blockEntityBigBellPart.Pos.Z);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
            int extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(blockEntityBigBellPart.Pos.X, blockEntityBigBellPart.Pos.Y, blockEntityBigBellPart.Pos.Z);
            prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(blockEntityBigBellPart.Pos.X - camPos.X, blockEntityBigBellPart.Pos.Y - camPos.Y, blockEntityBigBellPart.Pos.Z - camPos.Z)
                .Values;

            prog.RgbaLightIn = lightrgbs;
            prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);
            prog.ExtraGlow = extraGlow;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            if (blockEntityBigBellPart.BigBellPartMeshRef != null)
            {
                rpi.RenderMesh(blockEntityBigBellPart.BigBellPartMeshRef);
            }

            if (blockEntityBigBellPart.FluxMeshRef != null)
            {
                prog.ExtraGlow = 0;
                rpi.RenderMesh(blockEntityBigBellPart.FluxMeshRef);
            }

            prog.Stop();

        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }
    }
}