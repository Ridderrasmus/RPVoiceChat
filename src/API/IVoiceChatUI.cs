using RPVoiceChat.Audio;
using System;

namespace RPVoiceChat.API
{
    /// <summary>
    /// Interface for interacting with the voice chat system from UI components
    /// </summary>
    public interface IVoiceChatUI
    {
        /// <summary>
        /// Get whether a player is currently speaking
        /// </summary>
        bool IsPlayerTalking(string playerId);

        /// <summary>
        /// Enable or disable audio loopback for testing
        /// </summary>
        bool Loopback { get; set; }

        /// <summary>
        /// Get or set whether audio input is muted
        /// </summary>
        bool IsMuted { get; set; }

        /// <summary>
        /// Set the gain (volume) for a specific player
        /// </summary>
        void SetPlayerGain(string playerId, float gain);

        /// <summary>
        /// Get the gain (volume) for a specific player
        /// </summary>
        float GetPlayerGain(string playerId);

        /// <summary>
        /// Event fired when a player starts or stops talking
        /// </summary>
        event Action<string, bool> OnPlayerTalkingStateChanged;
        
        /// <summary>
        /// Get the voice source for a player
        /// </summary>
        IVoiceSource GetPlayerVoiceSource(string playerId);
    }
}
