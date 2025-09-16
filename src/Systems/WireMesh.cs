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

        /// <summary>
        /// Builds a wire mesh from pos1 to pos2
        /// </summary>
        /// <param name="pos1">First Anchor Point</param>
        /// <param name="pos2">Second Anchor Point</param>
        /// <param name="thickness">Thickness of wire</param>
        /// <returns>Generated Mesh</returns>
        static public MeshData MakeWireMesh(Vec3f pos1, Vec3f pos2, float thickness = 0.015f)
        {
            // Thickness of wire, defaults to 0.015?
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

            Vec3f pos;

            mesh_top.Flags.Fill(0);
            mesh_bot.Flags.Fill(0);
            mesh_side.Flags.Fill(0);
            mesh_side2.Flags.Fill(0);

            //Add vertices
            for (int j = 0; j <= nSec; j++)
            {
                float x = dPos.X / nSec * j;
                float y = dPos.Y / nSec * j;
                float z = dPos.Z / nSec * j;
                float l = (float)Math.Sqrt(x * x + y * y + z * z);
                float dy = Catenary(l / dist, 1, 0.75f);
                pos = new Vec3f(x, y + dy, z);


                float du = dist / 2 / t / nSec;
                int color = 1;
                mesh_top.AddVertex(pos1.X + pos.X - b.X * t, pos1.Y + pos.Y + t, pos1.Z + pos.Z - b.Z * t, j * du, 0, color);
                mesh_top.AddVertex(pos1.X + pos.X + b.X * t, pos1.Y + pos.Y + t, pos1.Z + pos.Z + b.Z * t, j * du, 1, color);


                mesh_bot.AddVertex(pos1.X + pos.X - b.X * t, pos1.Y + pos.Y - t, pos1.Z + pos.Z - b.Z * t, j * du, 0, color);
                mesh_bot.AddVertex(pos1.X + pos.X + b.X * t, pos1.Y + pos.Y - t, pos1.Z + pos.Z + b.Z * t, j * du, 1, color);

                mesh_side.AddVertex(pos1.X + pos.X - b.X * t, pos1.Y + pos.Y + t, pos1.Z + pos.Z - b.Z * t, j * du, 1, color);
                mesh_side.AddVertex(pos1.X + pos.X - b.X * t, pos1.Y + pos.Y - t, pos1.Z + pos.Z - b.Z * t, j * du, 0, color);


                mesh_side2.AddVertex(pos1.X + pos.X + b.X * t, pos1.Y + pos.Y + t, pos1.Z + pos.Z + b.Z * t, j * du, 1, color);
                mesh_side2.AddVertex(pos1.X + pos.X + b.X * t, pos1.Y + pos.Y - t, pos1.Z + pos.Z + b.Z * t, j * du, 0, color);


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
