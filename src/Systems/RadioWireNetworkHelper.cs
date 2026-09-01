using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Systems;

namespace RPVoiceChat.Systems
{
    public static class RadioWireNetworkHelper
    {
        public static IEnumerable<BEWireNode> GetReachableWiredNodes(BEWireNode start)
        {
            if (start == null)
            {
                yield break;
            }

            foreach (var node in WireNetworkHandler.GetReachableNodes(start))
            {
                if (node != null)
                {
                    yield return node;
                }
            }
        }

        public static BlockEntityRadioSupervisionConsole FindSupervisionConsole(BEWireNode start)
        {
            return GetReachableWiredNodes(start)
                .OfType<BlockEntityRadioSupervisionConsole>()
                .FirstOrDefault();
        }

        public static IEnumerable<BlockEntityRadioEmitter> FindEmitters(BEWireNode start)
        {
            return GetReachableWiredNodes(start).OfType<BlockEntityRadioEmitter>();
        }

        public static IEnumerable<BlockEntityRadioMicrophone> FindMicrophones(BEWireNode start)
        {
            return GetReachableWiredNodes(start).OfType<BlockEntityRadioMicrophone>();
        }

        public static IEnumerable<BlockEntitySpeaker> FindSpeakers(BEWireNode start)
        {
            return GetReachableWiredNodes(start).OfType<BlockEntitySpeaker>();
        }

        public static IEnumerable<BlockEntityRadioReceiver> FindReceivers(BEWireNode start)
        {
            return GetReachableWiredNodes(start).OfType<BlockEntityRadioReceiver>();
        }

        public static IEnumerable<BlockEntityRadioMixingConsole> FindMixingConsoles(BEWireNode start)
        {
            return GetReachableWiredNodes(start).OfType<BlockEntityRadioMixingConsole>();
        }

        public static bool HasOnAirMixingConsole(BEWireNode start)
        {
            if (start == null)
            {
                return false;
            }

            if (FindMixingConsoles(start).Any(console => console.IsOnAir))
            {
                return true;
            }

            // Mixing console may be unloaded while HLS program presence stays on-air.
            long networkId = start.NetworkUID;
            if (networkId == 0)
            {
                return false;
            }

            return RadioRfPresenceRegistry.GetPrograms()
                .Any(program => program.IsOnAir && program.NetworkId == networkId);
        }

        public static bool IsRadioWiredNetwork(BEWireNode start)
        {
            if (start == null)
            {
                return false;
            }

            return GetReachableWiredNodes(start).Any(node =>
                node is IWireTypedNode typed && WireNodeKindRules.IsRadioFamilyEndpoint(typed.WireNodeKind));
        }
    }
}
