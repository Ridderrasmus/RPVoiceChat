using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Systems
{
    /// <summary>
    /// Reduced Warning Beacon structure: 1 stone/brick block under the lucerne,
    /// then a 5×5 platform in front of that block (not under the lucerne), same Y level.
    /// "Center" = center of the 5×5 platform only (excluding the block under the lucerne).
    /// Reserved zone: 3×3×3 above the center of this platform (block placement forbidden).
    /// </summary>
    public static class WarningBeaconStructure
    {
        /// <summary>Width (X) of the 5×5 platform.</summary>
        public const int SizeX = 5;
        /// <summary>Depth (Z) of the 5×5 platform in front of the block under the lucerne.</summary>
        public const int SizeZ = 5;

        private static readonly List<BlockPos> _structurePositions = new List<BlockPos>();

        /// <summary>Center of the reserved 3×3 zone (above the platform): same XZ as platform center, Y=0 = first layer above platform. Used for the light.</summary>
        public static BlockPos CenterLocal => new BlockPos(0, 0, 3);

        /// <summary>Local positions of the base layer of the reserved 3×3×3 above the center of the 5×5 platform (Y=0 = above the platform). Height 0–2 is reserved.</summary>
        private static readonly List<BlockPos> _reservedLocal3x3 = new List<BlockPos>();

        static WarningBeaconStructure()
        {
            // 1 block under the lucerne: (0, -1, 0)
            _structurePositions.Add(new BlockPos(0, -1, 0));
            // 5×5 in front of that block (not the lucerne): Y=-1, x in [-2,2], z in [1,5]
            for (int x = -2; x <= 2; x++)
            for (int z = 1; z <= 5; z++)
                _structurePositions.Add(new BlockPos(x, -1, z));

            // 3×3 at center of the 5×5 platform (center = (0,3) in XZ, excluding block under lucerne), above: x in [-1,1], z in [2,4], Y=0
            for (int x = -1; x <= 1; x++)
            for (int z = 2; z <= 4; z++)
                _reservedLocal3x3.Add(new BlockPos(x, 0, z));
        }

        /// <summary>Local positions (excluding the lucerne) of the structure blocks: 1 under lucerne + 25 = 26.</summary>
        public static IReadOnlyList<BlockPos> StructurePositions => _structurePositions;

        /// <summary>Local positions of the base layer of the reserved 3×3×3 (one horizontal layer, Y=0 in local = above the platform).</summary>
        public static IReadOnlyList<BlockPos> ReservedLocal3x3 => _reservedLocal3x3;

        /// <summary>Converts a local offset to world position according to block orientation (side = north/east/south/west).</summary>
        public static BlockPos LocalToWorld(BlockPos lucernePos, int localX, int localY, int localZ, BlockFacing facing)
        {
            int wx = lucernePos.X, wy = lucernePos.Y + localY, wz = lucernePos.Z;
            switch (facing?.Code ?? "north")
            {
                case "north": wx += localX; wz -= localZ; break;
                case "south": wx -= localX; wz += localZ; break;
                case "east":  wx += localZ; wz += localX; break;
                case "west":  wx -= localZ; wz -= localX; break;
                default:      wx += localX; wz -= localZ; break;
            }
            return new BlockPos(wx, wy, wz);
        }

        /// <summary>Returns whether the block is considered stone/brick (stone-based construction).</summary>
        public static bool IsValidConstructionBlock(Block block)
        {
            if (block == null || block.Id == 0) return false;
            string path = (block.Code?.Path ?? "").ToLowerInvariant();
            string mat = (block.BlockMaterial.ToString() ?? "").ToLowerInvariant();
            if (mat == "stone" || mat == "rock") return true;
            if (path.Contains("stone") || path.Contains("brick") || path.Contains("cobble") || path.Contains("rock") || path.Contains("fireclay") || path.Contains("refractory")) return true;
            return false;
        }

        /// <summary>
        /// Returns whether the block is valid for the structure: stone/brick.
        /// For chisel/microblock blocks: at least 50% filled voxels required.
        /// </summary>
        public static bool SatisfiesChiselVoxelRequirement(IBlockAccessor accessor, BlockPos pos, Block block)
        {
            if (block == null || block.Id == 0) return false;
            if (!IsValidConstructionBlock(block)) return false;

            string path = block.Code?.Path ?? "";
            bool likelyChisel = path.IndexOf("microblock", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("chisel", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!likelyChisel) return true;

            var be = accessor.GetBlockEntity(pos);
            if (be == null) return false;

            int filled, maxVoxels;
            if (TryGetFilledVoxelCount(be, out filled, out maxVoxels))
                return maxVoxels > 0 && filled >= (maxVoxels / 2);

            return false;
        }

        private static bool TryGetFilledVoxelCount(object be, out int filled, out int maxVoxels)
        {
            filled = 0;
            maxVoxels = 0;
            if (be == null) return false;

            var type = be.GetType();
            var countProp = type.GetProperty("TotalVoxels") ?? type.GetProperty("VoxelCount") ?? type.GetProperty("FilledVoxels");
            if (countProp != null && countProp.PropertyType == typeof(int))
            {
                try
                {
                    filled = (int)countProp.GetValue(be);
                    maxVoxels = type.Name.IndexOf("MicroBlock", StringComparison.OrdinalIgnoreCase) >= 0 ? 512 : 4096;
                    return true;
                }
                catch { return false; }
            }

            var voxelsField = type.GetField("Voxels", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (voxelsField != null)
            {
                try
                {
                    var arr = voxelsField.GetValue(be) as Array;
                    if (arr != null && arr.Rank == 3)
                    {
                        maxVoxels = arr.Length;
                        int n = 0;
                        foreach (var v in arr)
                            if (v is bool b && b) n++;
                        filled = n;
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        public static bool ValidateStructure(IBlockAccessor accessor, BlockPos lucernePos, BlockFacing facing, Block lucerneBlock, out int filled, out int total)
        {
            total = _structurePositions.Count;
            filled = 0;
            foreach (var local in _structurePositions)
            {
                var worldPos = LocalToWorld(lucernePos, local.X, local.Y, local.Z, facing);
                var block = accessor.GetBlock(worldPos);
                if (SatisfiesChiselVoxelRequirement(accessor, worldPos, block))
                    filled++;
            }
            return filled == total;
        }

        // --- Reserved 3×3 zone above center: prevent block placement (patch checks this) ---

        private static readonly List<(BlockPos lucernePos, BlockFacing facing)> _registeredLucernes = new List<(BlockPos, BlockFacing)>();
        private static readonly object _lock = new object();

        /// <summary>Registers a lucerne (called by the BE on init / load).</summary>
        public static void RegisterLucerne(BlockPos lucernePos, BlockFacing facing)
        {
            lock (_lock)
            {
                for (int i = 0; i < _registeredLucernes.Count; i++)
                    if (_registeredLucernes[i].lucernePos.Equals(lucernePos)) return;
                _registeredLucernes.Add((lucernePos.Copy(), facing));
            }
        }

        /// <summary>Unregisters a lucerne (block removed / unloaded).</summary>
        public static void UnregisterLucerne(BlockPos lucernePos)
        {
            lock (_lock)
            {
                for (int i = _registeredLucernes.Count - 1; i >= 0; i--)
                    if (_registeredLucernes[i].lucernePos.Equals(lucernePos))
                        _registeredLucernes.RemoveAt(i);
            }
        }

        /// <summary>Returns whether a world position is inside the reserved 3×3×3 zone (above the center of the 5×5 platform).</summary>
        public static bool IsInReservedZone(IBlockAccessor accessor, BlockPos worldPos)
        {
            lock (_lock)
            {
                foreach (var (lucernePos, facing) in _registeredLucernes)
                {
                    foreach (var local in _reservedLocal3x3)
                    {
                        var w = LocalToWorld(lucernePos, local.X, local.Y, local.Z, facing);
                        // 3×3 in XZ, 3 blocks high: Y in [w.Y, w.Y+2]
                        if (w.X == worldPos.X && w.Z == worldPos.Z && worldPos.Y >= w.Y && worldPos.Y <= w.Y + 2)
                            return true;
                    }
                }
            }
            return false;
        }
    }
}
