using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RPVoiceChat.Systems
{
    /// <summary>
    /// Membership of a node on a radio network (parallel to wired <see cref="WireNetwork.PersistedNodes"/>).
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WirelessNetworkMembership
    {
        public long NetworkId;
        public List<TopologyNodeRef> Members = new List<TopologyNodeRef>();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WirelessTopologyData
    {
        public CommunicationTopologyData Links = new CommunicationTopologyData();
        public List<WirelessNetworkMembership> Networks = new List<WirelessNetworkMembership>();
    }

    /// <summary>
    /// World-level wireless communication topology.
    /// <para />
    /// Hybrid model:
    /// <list type="bullet">
    /// <item><description>Wired backbone: radio hubs / switchboards / machines use <see cref="WireTopologyRegistry"/>.</description></item>
    /// <item><description>Wireless overlay: antennas (blocks) and talkies (players) are affiliated to the same <c>networkId</c> here.</description></item>
    /// <item><description>Optional RF links (antenna ↔ talkie, antenna ↔ antenna) live in the generic graph with <see cref="TopologyLinkKind.Wireless"/>.</description></item>
    /// </list>
    /// Range/proximity checks remain runtime-only; this registry stores durable affiliations.
    /// </summary>
    public static class WirelessTopologyRegistry
    {
        public const string TopologyDataKey = "rpvc:wireless-topology";

        private static readonly CommunicationTopologyGraph graph = new CommunicationTopologyGraph();
        private static readonly Dictionary<long, HashSet<string>> membersByNetwork = new Dictionary<long, HashSet<string>>();
        private static readonly Dictionary<string, long> networkByMemberKey = new Dictionary<string, long>();

        public static int LinkCount => graph.LinkCount;

        public static void Clear()
        {
            graph.Clear();
            membersByNetwork.Clear();
            networkByMemberKey.Clear();
        }

        public static void LoadFromSave(byte[] data)
        {
            Clear();
            if (data == null || data.Length == 0)
            {
                return;
            }

            var payload = SerializerUtil.Deserialize<WirelessTopologyData>(data);
            if (payload == null)
            {
                return;
            }

            graph.LoadFromData(payload.Links ?? new CommunicationTopologyData());

            if (payload.Networks == null)
            {
                return;
            }

            foreach (var membership in payload.Networks)
            {
                if (membership == null || membership.NetworkId == 0 || membership.Members == null)
                {
                    continue;
                }

                foreach (var member in membership.Members)
                {
                    RegisterMember(membership.NetworkId, member, rebuildIndexes: false);
                }
            }
        }

        public static byte[] ToSaveBytes()
        {
            var payload = new WirelessTopologyData
            {
                Links = SerializerUtil.Deserialize<CommunicationTopologyData>(graph.ToSaveBytes()) ?? new CommunicationTopologyData(),
                Networks = membersByNetwork
                    .Select(entry => new WirelessNetworkMembership
                    {
                        NetworkId = entry.Key,
                        Members = entry.Value
                            .Select(TopologyNodeRef.FromKey)
                            .Where(node => node != null)
                            .ToList()
                    })
                    .ToList()
            };

            return SerializerUtil.Serialize(payload);
        }

        /// <summary>Registers a block antenna on a radio network (typically the wired <see cref="WireNetwork.networkID"/> of its hub).</summary>
        public static void RegisterAntenna(BlockPos antennaPos, long networkId)
        {
            RegisterMember(networkId, TopologyNodeRef.FromBlock(antennaPos));
        }

        /// <summary>Registers a handheld talkie owner on a radio network.</summary>
        public static void BindTalkie(string playerUid, long networkId)
        {
            RegisterMember(networkId, TopologyNodeRef.FromPlayer(playerUid));
        }

        public static void UnregisterNode(TopologyNodeRef node)
        {
            if (node == null || !node.IsValid)
            {
                return;
            }

            if (networkByMemberKey.TryGetValue(node.Key, out long networkId))
            {
                membersByNetwork.TryGetValue(networkId, out var members);
                members?.Remove(node.Key);
                if (members != null && members.Count == 0)
                {
                    membersByNetwork.Remove(networkId);
                }

                networkByMemberKey.Remove(node.Key);
            }

            graph.RemoveAllLinksAt(node, TopologyLinkKind.Wireless);
        }

        public static void UnregisterAntenna(BlockPos antennaPos)
        {
            UnregisterNode(TopologyNodeRef.FromBlock(antennaPos));
        }

        public static void UnbindTalkie(string playerUid)
        {
            UnregisterNode(TopologyNodeRef.FromPlayer(playerUid));
        }

        /// <summary>Explicit RF link (e.g. talkie paired to one antenna). Does not replace network membership.</summary>
        public static bool LinkWireless(TopologyNodeRef a, TopologyNodeRef b)
        {
            return graph.AddLink(a, b, TopologyLinkKind.Wireless);
        }

        public static bool UnlinkWireless(TopologyNodeRef a, TopologyNodeRef b)
        {
            return graph.RemoveLink(a, b, TopologyLinkKind.Wireless);
        }

        public static long ResolveNetworkId(TopologyNodeRef node)
        {
            if (node == null || !node.IsValid)
            {
                return 0;
            }

            return networkByMemberKey.TryGetValue(node.Key, out long networkId) ? networkId : 0;
        }

        public static long ResolveNetworkIdForAntenna(BlockPos antennaPos)
        {
            return ResolveNetworkId(TopologyNodeRef.FromBlock(antennaPos));
        }

        public static long ResolveNetworkIdForTalkie(string playerUid)
        {
            return ResolveNetworkId(TopologyNodeRef.FromPlayer(playerUid));
        }

        /// <summary>
        /// Resolves the radio network carried by a wired block (hub/antenna machine) through the wired graph.
        /// Bridges wired <see cref="WireNetworkHandler"/> with the wireless overlay.
        /// </summary>
        public static long ResolveNetworkIdFromWiredBlock(BlockPos blockPos)
        {
            if (blockPos == null)
            {
                return 0;
            }

            long wiredNetworkId = WireNetworkHandler.ResolveNetworkIdForPosition(blockPos);
            if (wiredNetworkId == 0)
            {
                return 0;
            }

            long wirelessNetworkId = ResolveNetworkId(TopologyNodeRef.FromBlock(blockPos));
            return wirelessNetworkId != 0 ? wirelessNetworkId : wiredNetworkId;
        }

        public static IEnumerable<TopologyNodeRef> GetMembers(long networkId)
        {
            if (networkId == 0 || !membersByNetwork.TryGetValue(networkId, out var members) || members == null)
            {
                yield break;
            }

            foreach (string memberKey in members)
            {
                yield return new TopologyNodeRef { Key = memberKey };
            }
        }

        public static IEnumerable<BlockPos> GetAntennas(long networkId)
        {
            foreach (var member in GetMembers(networkId))
            {
                BlockPos pos = member?.ToBlockPos();
                if (pos != null)
                {
                    yield return pos;
                }
            }
        }

        public static IEnumerable<string> GetBoundTalkies(long networkId)
        {
            foreach (var member in GetMembers(networkId))
            {
                string playerUid = member?.ToPlayerUid();
                if (!string.IsNullOrEmpty(playerUid))
                {
                    yield return playerUid;
                }
            }
        }

        public static HashSet<TopologyNodeRef> GetWirelessComponent(TopologyNodeRef start)
        {
            return graph.GetConnectedComponent(start, TopologyLinkKind.Wireless);
        }

        private static void RegisterMember(long networkId, TopologyNodeRef node, bool rebuildIndexes = true)
        {
            if (networkId == 0 || node == null || !node.IsValid)
            {
                return;
            }

            if (!membersByNetwork.TryGetValue(networkId, out var members))
            {
                members = new HashSet<string>();
                membersByNetwork[networkId] = members;
            }

            members.Add(node.Key);
            networkByMemberKey[node.Key] = networkId;

            if (rebuildIndexes)
            {
                // no-op hook for future index extensions
            }
        }
    }
}
