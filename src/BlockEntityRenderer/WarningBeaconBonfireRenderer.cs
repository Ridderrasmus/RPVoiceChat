using System;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Renderers
{
    /// <summary>
    /// Renders the bonfire or bonfire_ashes shape at the center of the warning beacon (above the platform).
    /// </summary>
    public class WarningBeaconBonfireRenderer : IRenderer, IDisposable
    {
        private const string ShapeBonfire = "shapes/block/warningbeacon/bonfire.json";
        private const string ShapeBonfireAshes = "shapes/block/warningbeacon/bonfire_ashes.json";

        private readonly ICoreClientAPI capi;
        private readonly BlockEntityLucerne lucerne;
        private MeshRef meshBonfire;
        private MeshRef meshAshes;
        private Vec3d origin;
        private bool lastShowAshes = true;
        private bool meshBuilt;

        public WarningBeaconBonfireRenderer(BlockEntityLucerne lucerne, ICoreClientAPI capi)
        {
            this.lucerne = lucerne;
            this.capi = capi;
            lastShowAshes = lucerne.ShowAshes;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "warningbeaconbonfire");
        }

        public double RenderOrder => 0.6;
        public int RenderRange => 80;

        public void UpdateState()
        {
            bool showAshes = lucerne.ShowAshes;
            if (showAshes != lastShowAshes)
            {
                lastShowAshes = showAshes;
                meshBuilt = false;
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque || lucerne == null) return;

            if (!meshBuilt)
                BuildMeshes();

            MeshRef current = lucerne.ShowAshes ? meshAshes : meshBonfire;
            if (current == null) return;

            BlockPos center = lucerne.GetStructureCenterWorldPos();
            origin = new Vec3d(lucerne.Pos.X, lucerne.Pos.Y, lucerne.Pos.Z);
            // Place shape so its base sits on the platform; center horizontally in the 3×3 (one block lower than center block)
            float dx = (float)(center.X - origin.X);
            float dy = (float)(center.Y - origin.Y);
            float dz = (float)(center.Z - origin.Z);

            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            IStandardShaderProgram prog = capi.Render.PreparedStandardShader(
                (int)origin.X, (int)origin.Y, (int)origin.Z);
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;
            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ModelMatrix = new Matrixf()
                .Identity()
                .Translate(
                    (float)(origin.X - camPos.X) + dx,
                    (float)(origin.Y - camPos.Y) + dy,
                    (float)(origin.Z - camPos.Z) + dz)
                .Values;

            prog.Tex2D = capi.BlockTextureAtlas?.AtlasTextures[0]?.TextureId ?? 0;
            prog.Use();
            capi.Render.RenderMesh(current);
            prog.Stop();
        }

        private void BuildMeshes()
        {
            meshBonfire?.Dispose();
            meshAshes?.Dispose();
            meshBonfire = null;
            meshAshes = null;

            var block = lucerne.Block;
            if (block == null) return;

            var locBonfire = new AssetLocation(RPVoiceChatMod.modID, ShapeBonfire);
            var locAshes = new AssetLocation(RPVoiceChatMod.modID, ShapeBonfireAshes);
            Shape shapeBonfire = Shape.TryGet(capi, locBonfire);
            Shape shapeAshes = Shape.TryGet(capi, locAshes);

            if (shapeBonfire != null)
            {
                capi.Tesselator.TesselateShape(block, shapeBonfire, out MeshData meshDataBonfire);
                if (meshDataBonfire?.VerticesCount > 0)
                    meshBonfire = capi.Render.UploadMesh(meshDataBonfire);
            }

            if (shapeAshes != null)
            {
                capi.Tesselator.TesselateShape(block, shapeAshes, out MeshData meshDataAshes);
                if (meshDataAshes?.VerticesCount > 0)
                    meshAshes = capi.Render.UploadMesh(meshDataAshes);
            }

            meshBuilt = true;
        }

        public void Dispose()
        {
            meshBonfire?.Dispose();
            meshAshes?.Dispose();
            meshBonfire = null;
            meshAshes = null;
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            GC.SuppressFinalize(this);
        }
    }
}
