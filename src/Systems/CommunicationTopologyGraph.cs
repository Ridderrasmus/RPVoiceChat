using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Util;

namespace RPVoiceChat.Systems
{
    public enum TopologyLinkKind
    {
        /// <summary>Physical wire between two block entities.</summary>
        Wired = 0,

        /// <summary>Logical RF link (antenna hub to talkie, antenna to antenna relay, etc.).</summary>
        Wireless = 1
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TopologyLink
    {
        public string NodeKeyA = "";
        public string NodeKeyB = "";
        public TopologyLinkKind Kind;

        public TopologyLink() { }

        public TopologyLink(TopologyNodeRef a, TopologyNodeRef b, TopologyLinkKind kind)
        {
            if (a == null || b == null || !a.IsValid || !b.IsValid || a.Key == b.Key)
            {
                NodeKeyA = "";
                NodeKeyB = "";
                return;
            }

            if (string.CompareOrdinal(a.Key, b.Key) <= 0)
            {
                NodeKeyA = a.Key;
                NodeKeyB = b.Key;
            }
            else
            {
                NodeKeyA = b.Key;
                NodeKeyB = a.Key;
            }

            Kind = kind;
        }

        [ProtoIgnore]
        public bool IsValid => !string.IsNullOrEmpty(NodeKeyA) && !string.IsNullOrEmpty(NodeKeyB) && NodeKeyA != NodeKeyB;

        public override bool Equals(object obj)
        {
            return obj is TopologyLink other
                && NodeKeyA == other.NodeKeyA
                && NodeKeyB == other.NodeKeyB
                && Kind == other.Kind;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(NodeKeyA, NodeKeyB, Kind);
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CommunicationTopologyData
    {
        public List<TopologyLink> Links = new List<TopologyLink>();
    }

    /// <summary>
    /// Generic undirected communication graph persisted at world level.
    /// Used by <see cref="WirelessTopologyRegistry"/> today; wired graphs can migrate here later.
    /// </summary>
    public class CommunicationTopologyGraph
    {
        private readonly HashSet<TopologyLink> links = new HashSet<TopologyLink>();
        private readonly Dictionary<string, HashSet<string>> adjacency = new Dictionary<string, HashSet<string>>();

        public int LinkCount => links.Count;

        public void Clear()
        {
            links.Clear();
            adjacency.Clear();
        }

        public void LoadFromBytes(byte[] data)
        {
            Clear();
            if (data == null || data.Length == 0)
            {
                return;
            }

            LoadFromData(SerializerUtil.Deserialize<CommunicationTopologyData>(data));
        }

        public void LoadFromData(CommunicationTopologyData payload)
        {
            Clear();
            if (payload?.Links == null)
            {
                return;
            }

            foreach (var link in payload.Links)
            {
                if (link == null || !link.IsValid)
                {
                    continue;
                }

                AddLink(TopologyNodeRef.FromKey(link.NodeKeyA), TopologyNodeRef.FromKey(link.NodeKeyB), link.Kind, rebuildAdjacency: false);
            }

            RebuildAdjacency();
        }

        public byte[] ToSaveBytes()
        {
            return SerializerUtil.Serialize(new CommunicationTopologyData
            {
                Links = links.ToList()
            });
        }

        public bool AddLink(TopologyNodeRef a, TopologyNodeRef b, TopologyLinkKind kind, bool rebuildAdjacency = true)
        {
            var link = new TopologyLink(a, b, kind);
            if (!link.IsValid || !links.Add(link))
            {
                return false;
            }

            if (rebuildAdjacency)
            {
                LinkNodes(link.NodeKeyA, link.NodeKeyB);
            }

            return true;
        }

        public bool RemoveLink(TopologyNodeRef a, TopologyNodeRef b, TopologyLinkKind kind)
        {
            if (a == null || b == null || !a.IsValid || !b.IsValid)
            {
                return false;
            }

            if (!links.Remove(new TopologyLink(a, b, kind)))
            {
                return false;
            }

            UnlinkNodes(a.Key, b.Key);
            return true;
        }

        public void RemoveAllLinksAt(TopologyNodeRef node, TopologyLinkKind? kindFilter = null)
        {
            if (node == null || !node.IsValid)
            {
                return;
            }

            foreach (var neighbor in GetNeighborNodes(node, kindFilter).ToArray())
            {
                TopologyLinkKind kind = ResolveLinkKind(node.Key, neighbor.Key) ?? TopologyLinkKind.Wireless;
                if (kindFilter == null || kindFilter == kind)
                {
                    RemoveLink(node, neighbor, kind);
                }
            }
        }

        public IEnumerable<TopologyNodeRef> GetNeighborNodes(TopologyNodeRef node, TopologyLinkKind? kindFilter = null)
        {
            if (node == null || !node.IsValid)
            {
                yield break;
            }

            if (!adjacency.TryGetValue(node.Key, out var neighbors) || neighbors == null)
            {
                yield break;
            }

            foreach (string neighborKey in neighbors)
            {
                if (kindFilter != null)
                {
                    TopologyLinkKind? kind = ResolveLinkKind(node.Key, neighborKey);
                    if (kind == null || kind != kindFilter)
                    {
                        continue;
                    }
                }

                yield return new TopologyNodeRef { Key = neighborKey };
            }
        }

        public HashSet<TopologyNodeRef> GetConnectedComponent(TopologyNodeRef start, TopologyLinkKind? kindFilter = null)
        {
            var component = new HashSet<TopologyNodeRef>();
            if (start == null || !start.IsValid)
            {
                return component;
            }

            var queue = new Queue<TopologyNodeRef>();
            queue.Enqueue(start);
            component.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in GetNeighborNodes(current, kindFilter))
                {
                    if (neighbor == null || !neighbor.IsValid || !component.Add(neighbor))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            return component;
        }

        private TopologyLinkKind? ResolveLinkKind(string nodeKeyA, string nodeKeyB)
        {
            return links.FirstOrDefault(link =>
                link.IsValid
                && ((link.NodeKeyA == nodeKeyA && link.NodeKeyB == nodeKeyB)
                    || (link.NodeKeyA == nodeKeyB && link.NodeKeyB == nodeKeyA)))?.Kind;
        }

        private void RebuildAdjacency()
        {
            adjacency.Clear();
            foreach (var link in links)
            {
                if (!link.IsValid)
                {
                    continue;
                }

                LinkNodes(link.NodeKeyA, link.NodeKeyB);
            }
        }

        private void LinkNodes(string keyA, string keyB)
        {
            if (string.IsNullOrEmpty(keyA) || string.IsNullOrEmpty(keyB))
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

        private void UnlinkNodes(string keyA, string keyB)
        {
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
    }
}
