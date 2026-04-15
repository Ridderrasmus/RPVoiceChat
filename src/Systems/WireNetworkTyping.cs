using RPVoiceChat.Config;

namespace RPVoiceChat.GameContent.Systems
{
    public enum WireNodeKind
    {
        Infrastructure = 0,
        Telegraph = 1,
        Telephone = 2,
        Switchboard = 3,
        Wireless = 4
    }

    public enum WireNetworkKind
    {
        None = 0,
        Telegraph = 1,
        Telephone = 2,
        Mixed = 3,
        Wireless = 4
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

        public static WireNetworkRequirements GetRequirements(WireNetworkKind kind)
        {
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
                case WireNetworkKind.Wireless:
                    return new WireNetworkRequirements(
                        PercentToNormalized(ServerConfigManager.WirelessNetworkMinPowerPercent),
                        ServerConfigManager.WirelessNetworkMaxEndpoints
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
}
