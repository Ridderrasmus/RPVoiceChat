using System;
using RPVoiceChat.GameContent.Blocks;
using RPVoiceChat.GameContent.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Renderers
{
    public class WireNodeRenderer : IRenderer, IDisposable
    {
        private readonly ICoreClientAPI capi;
        private readonly WireNode node;

        private MeshRef meshRef;
        private Vec3f meshOrigin;
        private bool needsRebuild = true;

        public WireNodeRenderer(WireNode node, ICoreClientAPI capi)
        {
            this.node = node;
            this.capi = capi;

            node.OnConnectionsChanged += () => MarkNeedsRebuild();
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "wirenoderenderer");
        }

        public double RenderOrder => 0.5;
        public int RenderRange => 100;

        public void MarkNeedsRebuild()
        {
            needsRebuild = true;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque || node == null) return;

            if (needsRebuild)
            {
                RebuildMesh();
            }

            if (meshRef == null) return;

            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            IStandardShaderProgram prog = capi.Render.PreparedStandardShader(
                (int)node.Pos.X, (int)node.Pos.Y, (int)node.Pos.Z
            );

            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;
            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;

            prog.ModelMatrix = new Matrixf()
                .Identity()
                .Translate(
                    meshOrigin.X - (float)camPos.X,
                    meshOrigin.Y - (float)camPos.Y,
                    meshOrigin.Z - (float)camPos.Z
                )
                .Values;

            int texId = capi.Render.GetOrLoadTexture(new AssetLocation("game:block/metal/plate/copper")); //TODO ajuster texture
            capi.Render.BindTexture2d(texId);

            prog.Use();
            capi.Render.RenderMesh(meshRef);
            prog.Stop();
        }

        private void RebuildMesh()
        {
            meshRef?.Dispose();
            meshRef = null;

            var connections = node.GetConnections();
            if (connections == null || connections.Count == 0) return;

            // Origin fixed in centre of block
            // TODO : add parameter to offset origin within block
            Vec3f origin = node.Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f);
            meshOrigin = origin;

            // Combined MeshData to render all wires at once
            MeshData combinedMesh = new MeshData(4, 6);
            combinedMesh.SetMode(EnumDrawMode.Triangles);

            foreach (var conn in connections)
            {
                var other = conn.GetOtherNode(node);
                if (other == null || other.Pos == null) continue;

                Vec3f startLocal = origin - origin; // always (0,0,0)
                Vec3f endLocal = other.Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f) - origin;

                MeshData wireMesh = WireMesh.MakeWireMesh(startLocal, endLocal, 0.05f);

                combinedMesh.AddMeshData(wireMesh);
            }

            if (combinedMesh.VerticesCount > 0)
            {
                // White plain color
                int vcount = combinedMesh.VerticesCount;
                combinedMesh.Rgba = new byte[vcount * 4];
                for (int i = 0; i < vcount; i++)
                {
                    combinedMesh.Rgba[i * 4 + 0] = 255;
                    combinedMesh.Rgba[i * 4 + 1] = 255;
                    combinedMesh.Rgba[i * 4 + 2] = 255;
                    combinedMesh.Rgba[i * 4 + 3] = 255;
                }

                meshRef = capi.Render.UploadMesh(combinedMesh);
            }

            needsRebuild = false;
        }

        public void Dispose()
        {
            meshRef?.Dispose();
            meshRef = null;
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            GC.SuppressFinalize(this);
        }
    }
}
