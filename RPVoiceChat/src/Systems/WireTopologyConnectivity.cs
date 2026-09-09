using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    /// <summary>
    /// Central entry point when wire topology changes. Dispatches validation to each traffic family
    /// (radio, telephone, etc.) without coupling <see cref="BEWireNode"/> to domain-specific logic.
    /// Event-driven only — no periodic polling.
    /// </summary>
    public static class WireTopologyConnectivity
    {
        public static void NotifyNode(ICoreServerAPI api, BEWireNode node)
        {
            if (api == null || node == null)
            {
                return;
            }

            NotifyAffectedNodes(api, new[] { node });
        }

        public static void NotifyAffectedNodes(ICoreServerAPI api, IEnumerable<BEWireNode> nodes)
        {
            if (api == null || nodes == null)
            {
                return;
            }

            RadioConnectivityValidator.ValidateNodesInComponents(nodes);
            TelephoneConnectivityValidator.ValidateNodesInComponents(nodes);
        }

        public static void NotifyComponents(
            ICoreServerAPI api,
            IEnumerable<BEWireNode> componentA,
            IEnumerable<BEWireNode> componentB)
        {
            if (api == null)
            {
                return;
            }

            var merged = new List<BEWireNode>();
            if (componentA != null)
            {
                merged.AddRange(componentA);
            }

            if (componentB != null)
            {
                merged.AddRange(componentB);
            }

            NotifyAffectedNodes(api, merged);
        }
    }
}
