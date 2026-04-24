namespace RPVoiceChat.GameContent.Systems
{
    /// <summary>
    /// Placeholder for future non-wired communication networks (e.g. radio).
    /// The concrete topology/membership model will be added with radio endpoints.
    /// </summary>
    public class RadioNetwork : CommunicationNetworkBase
    {
        public override NetworkTransportType TransportType => NetworkTransportType.Radio;
    }
}
