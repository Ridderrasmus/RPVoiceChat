using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace RPVoiceChat.GameContent.Renderers
{
    /// <summary>
    /// Quern-style dedicated renderer for a rotating mechanical sub-part.
    /// It renders one uploaded mesh and rotates it from MPConsumer.AngleRad.
    /// </summary>
    public class RotatingMechPartRenderer : IRenderer, IDisposable
    {
        private readonly ICoreClientAPI capi;
        private readonly Vintagestory.API.Common.BlockEntity blockEntity;
        private readonly AssetLocation shapeLoc;
        private readonly float baseRotYDeg;

        private MeshRef meshRef;

        public RotatingMechPartRenderer(Vintagestory.API.Common.BlockEntity blockEntity, ICoreClientAPI capi, AssetLocation shapeLoc, float baseRotYDeg)
        {
            this.blockEntity = blockEntity;
            this.capi = capi;
            this.shapeLoc = shapeLoc;
            this.baseRotYDeg = baseRotYDeg;

            BuildMesh();
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "rpvc-rotatingmechpart");
        }

        public double RenderOrder => 0.55;
        public int RenderRange => 80;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque || meshRef == null || blockEntity?.Api == null) return;

            var consumer = blockEntity.GetBehavior<BEBehaviorMPConsumer>();
            if (consumer == null) return;

            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            float angle = consumer.AngleRad % GameMath.TWOPI;
            int[] axis = consumer.AxisSign ?? new int[] { 0, 0, 1 };

            IStandardShaderProgram prog = capi.Render.PreparedStandardShader(blockEntity.Pos.X, blockEntity.Pos.Y, blockEntity.Pos.Z);
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;
            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ModelMatrix = new Matrixf()
                .Identity()
                .Translate(
                    (float)(blockEntity.Pos.X - camPos.X) + 0.5f,
                    (float)(blockEntity.Pos.Y - camPos.Y) + 0.5f,
                    (float)(blockEntity.Pos.Z - camPos.Z) + 0.5f
                )
                .RotateX(angle * axis[0])
                .RotateY(angle * axis[1])
                .RotateZ(angle * axis[2])
                .Translate(-0.5f, -0.5f, -0.5f)
                .Values;

            prog.Tex2D = capi.BlockTextureAtlas?.AtlasTextures[0]?.TextureId ?? 0;
            prog.Use();
            capi.Render.RenderMesh(meshRef);
            prog.Stop();
        }

        private void BuildMesh()
        {
            if (blockEntity?.Block == null) return;

            Shape shape = Shape.TryGet(capi, shapeLoc);
            if (shape == null) return;

            capi.Tesselator.TesselateShape(blockEntity.Block, shape, out MeshData meshData, new Vec3f(0f, baseRotYDeg, 0f));
            if (meshData?.VerticesCount > 0)
            {
                meshRef = capi.Render.UploadMesh(meshData);
            }
        }

        public void Dispose()
        {
            meshRef?.Dispose();
            meshRef = null;
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            GC.SuppressFinalize(this);
        }
    }
}
