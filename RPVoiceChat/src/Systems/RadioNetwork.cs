namespace RPVoiceChat.GameContent.Systems
{
    /// <summary>
    /// Radio communication network (wireless overlay).
    /// <para />
    /// Hybrid topology:
    /// <list type="bullet">
    /// <item><description>Wired backbone: radio machines/hubs/antenna blocks connect via <see cref="WireNetwork"/> + <c>WireTopologyRegistry</c>.</description></item>
    /// <item><description>Wireless overlay: antenna blocks and talkies affiliate to the same network id via <c>WirelessTopologyRegistry</c>.</description></item>
    /// </list>
    /// Gameplay routing (range, power, voice packets) will be added on top of this model.
    /// </summary>
    public class RadioNetwork : CommunicationNetworkBase
    {
        public override NetworkTransportType TransportType => NetworkTransportType.Radio;
    }
}
