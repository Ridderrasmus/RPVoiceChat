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

            int texId = capi.Render.GetOrLoadTexture(new AssetLocation("game:block/metal/plate/copper"));
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

            foreach (var conn in connections)
            {
                var other = conn.GetOtherNode(node);
                if (other == null || other.Pos == null) continue;

                Vec3f startWorld = node.Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f);
                Vec3f endWorld = other.Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f);

                // Choose a local origin for the mesh to minimize floating point precision issues
                meshOrigin = new Vec3f((float)startWorld.X, (float)startWorld.Y, (float)startWorld.Z);
                Vec3f startLocal = startWorld - meshOrigin;
                Vec3f endLocal = endWorld - meshOrigin;

                MeshData mesh = WireMesh.MakeWireMesh(startLocal, endLocal, 0.05f);
                mesh.SetMode(EnumDrawMode.Triangles);

                // Opaque color for each vertex
                int vcount = mesh.VerticesCount;
                mesh.Rgba = new byte[vcount * 4];
                for (int i = 0; i < vcount; i++)
                {
                    mesh.Rgba[i * 4 + 0] = 255;
                    mesh.Rgba[i * 4 + 1] = 255;
                    mesh.Rgba[i * 4 + 2] = 255;
                    mesh.Rgba[i * 4 + 3] = 255;
                }

                meshRef = capi.Render.UploadMesh(mesh);
                break; // only render one line
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
