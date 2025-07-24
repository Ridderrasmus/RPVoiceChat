using System;
using RPVoiceChat.Audio;
using RPVoiceChat.Networking;
using Vintagestory.API.Common.Entities;

namespace RPVoiceChat.API
{
    /// <summary>
    /// Represents a source of voice audio in the game world
    /// </summary>
    public interface IVoiceSource
    {
        /// <summary>
        /// Unique identifier for this voice source
        /// </summary>
        string SourceId { get; }

        /// <summary>
        /// Whether this voice source is currently playing audio
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// The current voice level of this source
        /// </summary>
        VoiceLevel VoiceLevel { get; }

        /// <summary>
        /// The position of this voice source in the world
        /// </summary>
        EntityPos Position { get; }

        /// <summary>
        /// Indicates if this source should be processed with positional audio
        /// </summary>
        bool IsLocational { get; }

        /// <summary>
        /// Event fired when audio data is received from this source
        /// </summary>
        event Action<AudioData> OnAudioReceived;

        /// <summary>
        /// Event fired when this source starts or stops being active
        /// </summary>
        event Action<bool> OnActiveStateChanged;

        /// <summary>
        /// Start processing audio from this source
        /// </summary>
        void Start();

        /// <summary>
        /// Stop processing audio from this source
        /// </summary>
        void Stop();

        /// <summary>
        /// Process incoming audio data for this source
        /// </summary>
        void HandleAudioPacket(AudioPacket packet);

        /// <summary>
        /// Update the source state (position, effects, etc)
        /// </summary>
        void Update();

        /// <summary>
        /// Update the gain (volume) for this voice source
        /// </summary>
        void UpdateGain(float gain);
    }
}
