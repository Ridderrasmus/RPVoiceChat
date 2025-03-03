using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPVoiceChat.src.Audio.AudioSources
{
    public interface IAudioSource
    {
        string SourceId { get; } // Unique identifier for this source (like player uid or block location)
        bool IsPlaying { get; } // Is the source currently playing audio
        float Volume { get; set; } // Volume of the source

        void PlayAudio(byte[] audioData, int frequency, ALFormat format); // Play audio data
        void StopAudio(); // Stop playing audio
    }
}
