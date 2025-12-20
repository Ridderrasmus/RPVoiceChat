namespace RPVoiceChat.GameContent.Systems
{
    /// <summary>
    /// Interface for blocks that can serve as network roots.
    /// Network roots are blocks that can create and maintain a network ID.
    /// </summary>
    public interface INetworkRoot
    {
        /// <summary>
        /// Gets the network ID that this root created.
        /// Returns 0 if this root hasn't created a network yet.
        /// </summary>
        long CreatedNetworkID { get; }
    }
}

