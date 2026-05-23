using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RPVoiceChat.GameContent.Systems
{
    /// <summary>
    /// RPVC edit : Thanks to VintageEngineering mod for this wire mesh generation ! Beauty of open-source !
    /// 
    /// Many Thanks to the Signals mod for help with the task of generating wire meshes.<br/>
    /// I could not find a better algorithm/implementation of this process.<br/>
    /// They won't say where they picked it up, but thank you. I had to unwind most of that mod<br/>
    /// and rebuild it from the ground up to ensure it could be used for any purpose.
    /// </summary>
    public class WireMesh
    {
        /// <summary>
        /// Calculate wire hang
        /// <br>Check https://en.wikipedia.org/wiki/Catenary </br>
        /// </summary>
        /// <param name="x">Horizontal Position</param>
        /// <param name="d">?</param>
        /// <param name="a">Amount of slump, smaller value = more slump but less smooth.</param>
        /// <returns>Vertical offset</returns>
        static float Catenary(float x, float d = 1, float a = 2)
        {
            return a * ((float)Math.Cosh((x - (d / 2)) / a) - (float)Math.Cosh((d / 2) / a));
        }

        private static Vec3f CrossProduct(Vec3f v1, Vec3f v2)
        {
            float x = v1.Y * v2.Z - v2.Y * v1.Z;
            float y = (v1.X * v2.Z - v2.X * v1.Z) * -1;
            float z = v1.X * v2.Y - v2.X * v1.Y;

            var result = new Vec3f(x, y, z);
            result.Normalize();
            return result;
        }

        /// <summary>
        /// Builds a wire mesh from pos1 to pos2
        /// </summary>
        /// <param name="pos1">First Anchor Point</param>
        /// <param name="pos2">Second Anchor Point</param>
        /// <param name="thickness">Thickness of wire</param>
        /// <returns>Generated Mesh</returns>
        static public MeshData MakeWireMesh(Vec3f pos1, Vec3f pos2, float thickness = 0.015f)
        {
            float t = thickness;
            Vec3f dPos = pos2 - pos1;
            float dist = pos2.DistanceTo(pos1);

            // Number of Sections
            int nSec = (int)Math.Floor(dist * 2);
            nSec = nSec > 5 ? nSec : 5;

            MeshData mesh = new MeshData(4, 6);
            mesh.SetMode(EnumDrawMode.Triangles);

            MeshData mesh_top = new MeshData(4, 6);
            mesh_top.SetMode(EnumDrawMode.Triangles);

            MeshData mesh_bot = new MeshData(4, 6);
            mesh_bot.SetMode(EnumDrawMode.Triangles);

            MeshData mesh_side = new MeshData(4, 6);
            mesh_side.SetMode(EnumDrawMode.Triangles);

            MeshData mesh_side2 = new MeshData(4, 6);
            mesh_side2.SetMode(EnumDrawMode.Triangles);

            //out of plane translation vector:
            Vec3f b = new Vec3f(-dPos.Z, 0, dPos.X).Normalize();
            if (dPos.Z == 0 && dPos.X == 0)
            {
                b = new Vec3f(1, 0, 0);
            }

            mesh_top.Flags.Fill(0);
            mesh_bot.Flags.Fill(0);
            mesh_side.Flags.Fill(0);
            mesh_side2.Flags.Fill(0);

            Vec3f[] positions = new Vec3f[nSec + 1];
            for (int j = 0; j <= nSec; j++)
            {
                float x = dPos.X / nSec * j;
                float y = dPos.Y / nSec * j;
                float z = dPos.Z / nSec * j;
                float l = (float)Math.Sqrt(x * x + y * y + z * z);
                float dy = Catenary(l / dist, 1, 2f);
                positions[j] = new Vec3f(x, y + dy, z);
            }

            Vec3f pos;
            Vec3f posNext;
            Vec3f posBefore;
            Vec3f direction;
            Vec3f a;

            // Add vertices
            for (int j = 0; j <= nSec; j++)
            {
                pos = pos1 + positions[j];
                posNext = j < nSec ? positions[j + 1] : positions[j];
                posBefore = j > 0 ? positions[j - 1] : positions[j];
                direction = (posNext + posBefore).Normalize();
                a = CrossProduct(direction, b * -1);

                float du = dist / nSec;
                int color = 1;
                float uvV = 2f / 16f;
                mesh_top.AddVertex(
                    (pos - b * t + a * t).X,
                    (pos - b * t + a * t).Y,
                    (pos - b * t + a * t).Z,
                    j * du, 0, color
                );
                mesh_top.AddVertex(
                    (pos + b * t + a * t).X,
                    (pos + b * t + a * t).Y,
                    (pos + b * t + a * t).Z,
                    j * du, uvV, color
                );

                mesh_bot.AddVertex(
                    (pos - b * t - a * t).X,
                    (pos - b * t - a * t).Y,
                    (pos - b * t - a * t).Z,
                    j * du, 0, color
                );
                mesh_bot.AddVertex(
                    (pos + b * t - a * t).X,
                    (pos + b * t - a * t).Y,
                    (pos + b * t - a * t).Z,
                    j * du, uvV, color
                );

                mesh_side.AddVertex(
                    (pos - b * t + a * t).X,
                    (pos - b * t + a * t).Y,
                    (pos - b * t + a * t).Z,
                    j * du, uvV, color
                );
                mesh_side.AddVertex(
                    (pos - b * t - a * t).X,
                    (pos - b * t - a * t).Y,
                    (pos - b * t - a * t).Z,
                    j * du, 0, color
                );

                mesh_side2.AddVertex(
                    (pos + b * t + a * t).X,
                    (pos + b * t + a * t).Y,
                    (pos + b * t + a * t).Z,
                    j * du, uvV, color
                );
                mesh_side2.AddVertex(
                    (pos + b * t - a * t).X,
                    (pos + b * t - a * t).Y,
                    (pos + b * t - a * t).Z,
                    j * du, 0, color
                );


                mesh_top.Flags[2 * j] = VertexFlags.PackNormal(new Vec3f(0, 1, 0));
                mesh_top.Flags[2 * j + 1] = VertexFlags.PackNormal(new Vec3f(0, 1, 0));

                mesh_bot.Flags[2 * j] = VertexFlags.PackNormal(new Vec3f(0, -1, 0));
                mesh_bot.Flags[2 * j + 1] = VertexFlags.PackNormal(new Vec3f(0, -1, 0));

                mesh_side.Flags[2 * j] = VertexFlags.PackNormal(-b.X, -b.Y, -b.Z);
                mesh_side.Flags[2 * j + 1] = VertexFlags.PackNormal(-b.X, -b.Y, -b.Z);

                mesh_side2.Flags[2 * j] = VertexFlags.PackNormal(b);
                mesh_side2.Flags[2 * j + 1] = VertexFlags.PackNormal(b);

            }

            //add indices
            for (int j = 0; j < nSec; j++)
            {
                //upper stripe
                int offset = 2 * j;
                mesh_top.AddIndex(offset);
                mesh_top.AddIndex(offset + 3);
                mesh_top.AddIndex(offset + 2);
                mesh_top.AddIndex(offset);
                mesh_top.AddIndex(offset + 1);
                mesh_top.AddIndex(offset + 3);

                //lower stripe
                mesh_bot.AddIndex(offset);
                mesh_bot.AddIndex(offset + 3);
                mesh_bot.AddIndex(offset + 1);
                mesh_bot.AddIndex(offset);
                mesh_bot.AddIndex(offset + 2);
                mesh_bot.AddIndex(offset + 3);

                //sides 
                mesh_side.AddIndex(offset);
                mesh_side.AddIndex(offset + 3);
                mesh_side.AddIndex(offset + 1);
                mesh_side.AddIndex(offset);
                mesh_side.AddIndex(offset + 2);
                mesh_side.AddIndex(offset + 3);


                mesh_side2.AddIndex(offset);
                mesh_side2.AddIndex(offset + 3);
                mesh_side2.AddIndex(offset + 2);
                mesh_side2.AddIndex(offset);
                mesh_side2.AddIndex(offset + 1);
                mesh_side2.AddIndex(offset + 3);

            }

            mesh.AddMeshData(mesh_top);
            mesh.AddMeshData(mesh_bot);
            mesh.AddMeshData(mesh_side);
            mesh.AddMeshData(mesh_side2);
            mesh.Rgba.Fill((byte)255);

            return mesh;
        }
    }
}
