using RPVoiceChat.Config;

namespace RPVoiceChat.GameContent.Systems
{
    /// <summary>
    /// Role of one block on the wire graph (passive node, telegraph key, telephone, switchboard hub, radio endpoint, …).
    /// </summary>
    public enum WireNodeKind
    {
        Infrastructure = 0,
        Telegraph = 1,
        Telephone = 2,
        Radio = 3,
        Switchboard = 4
    }

    /// <summary>
    /// Classification of the whole connected component for capacity and power rules
    /// (one traffic family, or <see cref="WireNetworkKind.None"/>).
    /// Kept separate from <see cref="WireNodeKind"/> because node kinds include infrastructure
    /// and switchboard (not a traffic family), and network kind adds None which is not a block role.
    /// </summary>
    public enum WireNetworkKind
    {
        None = 0,
        Telegraph = 1,
        Telephone = 2,
        Radio = 3
    }

    public readonly struct WireNetworkRequirements
    {
        public readonly float MinPowerPercent;
        public readonly int MaxEndpoints;

        public WireNetworkRequirements(float minPowerPercent, int maxEndpoints)
        {
            MinPowerPercent = minPowerPercent;
            MaxEndpoints = maxEndpoints;
        }
    }

    public static class WireNetworkTypeRules
    {
        private static float PercentToNormalized(int valuePercent)
        {
            return valuePercent <= 0 ? 0f : valuePercent / 100f;
        }

        /// <summary>
        /// None must not use the default rule (0% min) for power checks.
        /// Fall back to telegraph rules.
        /// </summary>
        public static WireNetworkKind ResolveKindForSwitchboardPowerCheck(WireNetworkKind kind)
        {
            if (kind == WireNetworkKind.None)
            {
                return WireNetworkKind.Telegraph;
            }

            return kind;
        }

        public static WireNetworkRequirements GetRequirements(WireNetworkKind kind)
        {
            kind = ResolveKindForSwitchboardPowerCheck(kind);
            switch (kind)
            {
                case WireNetworkKind.Telegraph:
                    return new WireNetworkRequirements(
                        PercentToNormalized(ServerConfigManager.TelegraphNetworkMinPowerPercent),
                        ServerConfigManager.TelegraphNetworkMaxEndpoints
                    );
                case WireNetworkKind.Telephone:
                    return new WireNetworkRequirements(
                        PercentToNormalized(ServerConfigManager.TelephoneNetworkMinPowerPercent),
                        ServerConfigManager.TelephoneNetworkMaxEndpoints
                    );
                case WireNetworkKind.Radio:
                    return new WireNetworkRequirements(
                        PercentToNormalized(ServerConfigManager.RadioNetworkMinPowerPercent),
                        ServerConfigManager.RadioNetworkMaxEndpoints
                    );
                default:
                    return new WireNetworkRequirements(0f, 0);
            }
        }
    }

    public interface IWireTypedNode
    {
        WireNodeKind WireNodeKind { get; }
    }

    public interface ISwitchboardNode
    {
        bool HasSufficientPowerFor(WireNetworkKind networkKind);
    }

    public interface ITelegraphEndpoint
    {
        string CustomEndpointName { get; }
    }

    public interface ITelephoneVoiceEndpoint
    {
        int VoiceEmissionRangeBlocks { get; }
    }
}
