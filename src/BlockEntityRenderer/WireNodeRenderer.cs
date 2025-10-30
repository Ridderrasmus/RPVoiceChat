using System;
using RPVoiceChat.GameContent.Block;
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
        // Default wire texture (atlas tile). Can be changed in future for different wire types.
        private AssetLocation wireTextureAsset = new AssetLocation("game:block/metal/plate/copper");

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

            // Use the block atlas to texture the wires (UVs remapped in RebuildMesh)
            prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

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

            Vec3f origin = node.Pos.ToVec3f().Add(node.WireAttachmentOffset);
            meshOrigin = origin;

            // Combined MeshData to render all wires at once
            MeshData combinedMesh = new MeshData(4, 6);
            combinedMesh.SetMode(EnumDrawMode.Triangles);

            foreach (var conn in connections)
            {
                var other = conn.GetOtherNode(node);
                if (other == null || other.Pos == null) continue;

                Vec3f startLocal = origin - origin; // always (0,0,0) in local space
                Vec3f endLocal = other.Pos.ToVec3f().Add(other.WireAttachmentOffset) - origin;

                MeshData wireMesh = WireMesh.MakeWireMesh(startLocal, endLocal, 0.05f);

                combinedMesh.AddMeshData(wireMesh);
            }

            if (combinedMesh.VerticesCount > 0)
            {
                // Remap UVs to the copper tile in the block atlas
                TextureAtlasPosition texPos;
                int subId;
                var ok = capi.BlockTextureAtlas.GetOrInsertTexture(wireTextureAsset, out subId, out texPos);
                if (ok && combinedMesh.Uv != null && combinedMesh.Uv.Length >= 2)
                {
                    for (int idx = 0; idx < combinedMesh.Uv.Length - 1; idx += 2)
                    {
                        float u = combinedMesh.Uv[idx];
                        float v = combinedMesh.Uv[idx + 1];
                        // Normalize to avoid sampling outside the target atlas tile (WireMesh can generate u > 1)
                        u = (float)(u - Math.Floor(u));
                        v = GameMath.Clamp(v, 0f, 1f);
                        combinedMesh.Uv[idx] = GameMath.Lerp(texPos.x1, texPos.x2, u);
                        combinedMesh.Uv[idx + 1] = GameMath.Lerp(texPos.y1, texPos.y2, v);
                    }
                }

                // White vertex color (modulated by the texture)
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
