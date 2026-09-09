using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RPVoiceChat.Systems
{
    /// <summary>
    /// Undirected wire edge between two block positions. Canonical ordering for stable equality.
    /// Inspired by VintageEngineering <c>CatenaryMod</c> world-level wire topology.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WireEdge
    {
        public BlockPos PosA;
        public BlockPos PosB;

        public WireEdge() { }

        public WireEdge(BlockPos a, BlockPos b)
        {
            if (ComparePos(a, b) <= 0)
            {
                PosA = a?.Copy();
                PosB = b?.Copy();
            }
            else
            {
                PosA = b?.Copy();
                PosB = a?.Copy();
            }
        }

        public bool Connects(BlockPos pos)
        {
            return pos != null && (pos.Equals(PosA) || pos.Equals(PosB));
        }

        public BlockPos GetOther(BlockPos pos)
        {
            if (pos == null || PosA == null || PosB == null)
            {
                return null;
            }

            if (pos.Equals(PosA))
            {
                return PosB.Copy();
            }

            if (pos.Equals(PosB))
            {
                return PosA.Copy();
            }

            return null;
        }

        public override bool Equals(object obj)
        {
            return obj is WireEdge other
                && PosA != null && PosB != null
                && other.PosA != null && other.PosB != null
                && PosA.Equals(other.PosA) && PosB.Equals(other.PosB);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PosA?.GetHashCode() ?? 0, PosB?.GetHashCode() ?? 0);
        }

        private static int ComparePos(BlockPos a, BlockPos b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            int cmp = a.X.CompareTo(b.X);
            if (cmp != 0) return cmp;
            cmp = a.Y.CompareTo(b.Y);
            if (cmp != 0) return cmp;
            return a.Z.CompareTo(b.Z);
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WireTopologyData
    {
        public List<WireEdge> Edges = new List<WireEdge>();
    }

    /// <summary>
    /// World-level registry of wire edges. Topology exists regardless of chunk load state.
    /// </summary>
    public static class WireTopologyRegistry
    {
        public const string TopologyDataKey = "rpvc:wire-topology";

        private static readonly HashSet<WireEdge> edges = new HashSet<WireEdge>();
        private static readonly Dictionary<string, HashSet<string>> adjacency = new Dictionary<string, HashSet<string>>();

        public static int EdgeCount => edges.Count;

        private static string PosKey(BlockPos pos)
        {
            return pos == null ? "" : $"{pos.X}|{pos.Y}|{pos.Z}";
        }

        public static void Clear()
        {
            edges.Clear();
            adjacency.Clear();
        }

        public static void LoadFromSave(byte[] data)
        {
            Clear();
            if (data == null || data.Length == 0)
            {
                return;
            }

            var topology = SerializerUtil.Deserialize<WireTopologyData>(data);
            if (topology?.Edges == null)
            {
                return;
            }

            foreach (var edge in topology.Edges)
            {
                if (edge?.PosA == null || edge?.PosB == null || edge.PosA.Equals(edge.PosB))
                {
                    continue;
                }

                AddEdge(edge.PosA, edge.PosB, rebuildAdjacency: false);
            }

            RebuildAdjacency();
        }

        public static byte[] ToSaveBytes()
        {
            return SerializerUtil.Serialize(new WireTopologyData
            {
                Edges = new List<WireEdge>(edges)
            });
        }

        public static bool AddEdge(BlockPos a, BlockPos b, bool rebuildAdjacency = true)
        {
            if (a == null || b == null || a.Equals(b))
            {
                return false;
            }

            var edge = new WireEdge(a, b);
            if (!edges.Add(edge))
            {
                return false;
            }

            if (rebuildAdjacency)
            {
                Link(a.Copy(), b.Copy());
            }

            return true;
        }

        public static bool RemoveEdge(BlockPos a, BlockPos b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (!edges.Remove(new WireEdge(a, b)))
            {
                return false;
            }

            Unlink(a, b);
            return true;
        }

        public static void RemoveAllEdgesAt(BlockPos pos)
        {
            if (pos == null)
            {
                return;
            }

            var neighbors = GetNeighborPositions(pos).ToArray();
            foreach (var neighbor in neighbors)
            {
                RemoveEdge(pos, neighbor);
            }
        }

        public static IEnumerable<BlockPos> GetNeighborPositions(BlockPos pos)
        {
            if (pos == null)
            {
                yield break;
            }

            string key = PosKey(pos);
            if (!adjacency.TryGetValue(key, out var neighbors) || neighbors == null)
            {
                yield break;
            }

            foreach (string neighborKey in neighbors)
            {
                var neighbor = ParsePosKey(neighborKey);
                if (neighbor != null)
                {
                    yield return neighbor;
                }
            }
        }

        public static HashSet<BlockPos> GetConnectedComponent(BlockPos start)
        {
            var component = new HashSet<BlockPos>();
            if (start == null)
            {
                return component;
            }

            var queue = new Queue<BlockPos>();
            var startCopy = start.Copy();
            queue.Enqueue(startCopy);
            component.Add(startCopy);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in GetNeighborPositions(current))
                {
                    if (neighbor == null || !component.Add(neighbor.Copy()))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbor.Copy());
                }
            }

            return component;
        }

        private static void RebuildAdjacency()
        {
            adjacency.Clear();
            foreach (var edge in edges)
            {
                if (edge?.PosA == null || edge?.PosB == null)
                {
                    continue;
                }

                Link(edge.PosA.Copy(), edge.PosB.Copy());
            }
        }

        private static void Link(BlockPos a, BlockPos b)
        {
            string keyA = PosKey(a);
            string keyB = PosKey(b);
            if (keyA.Length == 0 || keyB.Length == 0)
            {
                return;
            }

            if (!adjacency.TryGetValue(keyA, out var aNeighbors))
            {
                aNeighbors = new HashSet<string>();
                adjacency[keyA] = aNeighbors;
            }

            if (!adjacency.TryGetValue(keyB, out var bNeighbors))
            {
                bNeighbors = new HashSet<string>();
                adjacency[keyB] = bNeighbors;
            }

            aNeighbors.Add(keyB);
            bNeighbors.Add(keyA);
        }

        private static void Unlink(BlockPos a, BlockPos b)
        {
            string keyA = PosKey(a);
            string keyB = PosKey(b);
            if (keyA.Length == 0 || keyB.Length == 0)
            {
                return;
            }

            if (adjacency.TryGetValue(keyA, out var aNeighbors))
            {
                aNeighbors.Remove(keyB);
                if (aNeighbors.Count == 0)
                {
                    adjacency.Remove(keyA);
                }
            }

            if (adjacency.TryGetValue(keyB, out var bNeighbors))
            {
                bNeighbors.Remove(keyA);
                if (bNeighbors.Count == 0)
                {
                    adjacency.Remove(keyB);
                }
            }
        }

        private static BlockPos ParsePosKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            string[] parts = key.Split('|');
            if (parts.Length != 3)
            {
                return null;
            }

            if (!int.TryParse(parts[0], out int x)
                || !int.TryParse(parts[1], out int y)
                || !int.TryParse(parts[2], out int z))
            {
                return null;
            }

            return new BlockPos(x, y, z);
        }
    }
}
