namespace RPVoiceChat.GameContent.Systems
{
    public enum NetworkTransportType
    {
        Wired = 0,
        Radio = 1
    }

    public abstract class CommunicationNetworkBase
    {
        public long networkID;
        public string CustomName { get; private set; } = "";
        public abstract NetworkTransportType TransportType { get; }

        public void SetCustomName(string name)
        {
            CustomName = (name ?? "").Trim();
        }
    }
}
