using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Systems
{
    /// <summary>
    /// Kind of endpoint in a communication topology graph.
    /// Wired networks use <see cref="Block"/> only; wireless adds <see cref="Player"/> talkies and optional <see cref="Entity"/> devices.
    /// </summary>
    public enum TopologyNodeKind
    {
        Block = 0,
        Player = 1,
        Entity = 2
    }

    /// <summary>
    /// Canonical identity for a node in a generic communication topology.
    /// Keys are stable across chunk load/unload and world save.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TopologyNodeRef
    {
        public TopologyNodeKind Kind;
        public string Key = "";

        public TopologyNodeRef() { }

        public TopologyNodeRef(TopologyNodeKind kind, string key)
        {
            Kind = kind;
            Key = key ?? "";
        }

        public static TopologyNodeRef FromBlock(BlockPos pos)
        {
            if (pos == null)
            {
                return null;
            }

            return new TopologyNodeRef(TopologyNodeKind.Block, $"block:{pos.X}|{pos.Y}|{pos.Z}");
        }

        public static TopologyNodeRef FromPlayer(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return null;
            }

            return new TopologyNodeRef(TopologyNodeKind.Player, $"player:{playerUid}");
        }

        public static TopologyNodeRef FromEntity(long entityId)
        {
            return new TopologyNodeRef(TopologyNodeKind.Entity, $"entity:{entityId}");
        }

        public static TopologyNodeRef FromKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (key.StartsWith("block:"))
            {
                return new TopologyNodeRef(TopologyNodeKind.Block, key);
            }

            if (key.StartsWith("player:"))
            {
                return new TopologyNodeRef(TopologyNodeKind.Player, key);
            }

            if (key.StartsWith("entity:"))
            {
                return new TopologyNodeRef(TopologyNodeKind.Entity, key);
            }

            return new TopologyNodeRef(TopologyNodeKind.Block, key);
        }

        public BlockPos ToBlockPos()
        {
            if (Kind != TopologyNodeKind.Block || string.IsNullOrEmpty(Key) || !Key.StartsWith("block:"))
            {
                return null;
            }

            string[] parts = Key.Substring("block:".Length).Split('|');
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

        public string ToPlayerUid()
        {
            if (Kind != TopologyNodeKind.Player || string.IsNullOrEmpty(Key) || !Key.StartsWith("player:"))
            {
                return null;
            }

            return Key.Substring("player:".Length);
        }

        public bool IsValid => !string.IsNullOrEmpty(Key);

        public override bool Equals(object obj)
        {
            return obj is TopologyNodeRef other && Kind == other.Kind && Key == other.Key;
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Kind, Key);
        }

        public override string ToString() => $"{Kind}:{Key}";
    }
}
