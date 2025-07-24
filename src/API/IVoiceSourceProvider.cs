namespace RPVoiceChat.API
{
    /// <summary>
    /// Interface for creating voice sources. Implementations can create different types of voice sources.
    /// </summary>
    public interface IVoiceSourceProvider
    {
        /// <summary>
        /// Create a new voice source with the given ID
        /// </summary>
        IVoiceSource CreateVoiceSource(string sourceId);
    }
}
