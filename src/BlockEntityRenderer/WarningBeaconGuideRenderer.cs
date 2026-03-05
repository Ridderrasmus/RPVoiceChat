using System;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Renderers
{
    /// <summary>
    /// Renders the Warning Beacon structure guide (blocks to place) when ShowStructureGuide is active.
    /// </summary>
    public class WarningBeaconGuideRenderer : IRenderer, IDisposable
    {
        private readonly ICoreClientAPI capi;
        private readonly BlockEntityLucerne lucerne;
        private MeshRef meshRef;
        private Vec3d origin;
        private bool needsRebuild = true;

        public WarningBeaconGuideRenderer(BlockEntityLucerne lucerne, ICoreClientAPI capi)
        {
            this.lucerne = lucerne;
            this.capi = capi;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "warningbeaconguide");
        }

        public double RenderOrder => 0.6;
        public int RenderRange => 80;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque || lucerne == null) return;
            if (!lucerne.ShowStructureGuide || lucerne.StructureComplete) return;

            if (needsRebuild)
                RebuildMesh();

            if (meshRef == null) return;

            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            IStandardShaderProgram prog = capi.Render.PreparedStandardShader(
                (int)origin.X, (int)origin.Y, (int)origin.Z);
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;
            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ModelMatrix = new Matrixf()
                .Identity()
                .Translate(
                    (float)(origin.X - camPos.X),
                    (float)(origin.Y - camPos.Y),
                    (float)(origin.Z - camPos.Z))
                .Values;

            prog.Tex2D = capi.BlockTextureAtlas?.AtlasTextures[0]?.TextureId ?? 0;
            prog.Use();
            capi.Render.RenderMesh(meshRef);
            prog.Stop();
        }

        private void RebuildMesh()
        {
            meshRef?.Dispose();
            meshRef = null;

            var facing = lucerne.Block?.Variant?["side"] != null
                ? BlockFacing.FromCode((string)lucerne.Block.Variant["side"])
                : BlockFacing.NORTH;

            origin = new Vec3d(lucerne.Pos.X, lucerne.Pos.Y, lucerne.Pos.Z);
            MeshData combined = new MeshData(4, 6);
            combined.SetMode(EnumDrawMode.Triangles);

            // Neutral texture for uniform rendering (avoids odd atlas tint)
            TextureAtlasPosition texPos = default;
            int subId;
            bool hasTex = capi.BlockTextureAtlas.GetOrInsertTexture(new AssetLocation("game:block/stone/brick/plain"), out subId, out texPos);

            foreach (var local in WarningBeaconStructure.StructurePositions)
            {
                BlockPos w = WarningBeaconStructure.LocalToWorld(lucerne.Pos, local.X, local.Y, local.Z, facing);
                float lx = (float)(w.X - origin.X);
                float ly = (float)(w.Y - origin.Y);
                float lz = (float)(w.Z - origin.Z);
                MeshData box = CubeMeshUtil.GetCube();
                box.Scale(new Vec3f(0, 0, 0), 0.5f, 0.5f, 0.5f);
                box.Translate(lx + 0.5f, ly + 0.5f, lz + 0.5f);
                if (hasTex && box.Uv != null)
                {
                    for (int u = 0; u < box.Uv.Length; u += 2)
                    {
                        float uu = box.Uv[u]; float vv = box.Uv[u + 1];
                        uu = (float)(uu - Math.Floor(uu)); vv = GameMath.Clamp(vv, 0f, 1f);
                        box.Uv[u] = GameMath.Lerp(texPos.x1, texPos.x2, uu);
                        box.Uv[u + 1] = GameMath.Lerp(texPos.y1, texPos.y2, vv);
                    }
                }
                combined.AddMeshData(box);
            }

            if (combined.VerticesCount > 0)
            {
                int vc = combined.VerticesCount;
                combined.Rgba = new byte[vc * 4];
                // Semi-transparent grey-blue tint for the guide
                byte R = 200, G = 215, B = 230, A = 100;
                for (int i = 0; i < vc; i++)
                {
                    combined.Rgba[i * 4 + 0] = R;
                    combined.Rgba[i * 4 + 1] = G;
                    combined.Rgba[i * 4 + 2] = B;
                    combined.Rgba[i * 4 + 3] = A;
                }
                meshRef = capi.Render.UploadMesh(combined);
            }

            needsRebuild = false;
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
