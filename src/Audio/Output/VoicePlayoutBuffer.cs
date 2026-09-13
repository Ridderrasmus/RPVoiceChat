using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RPVoiceChat.Audio
{
    /// <summary>Orders speech by capture time and schedules it against a local monotonic clock.</summary>
    internal sealed class VoicePlayoutBuffer
    {
        private readonly SortedList<long, Entry> queue = new();
        private readonly HashSet<string> retiredSessions = new();
        private string session;
        private double anchorMedia, anchorLocal, lastArrival, lastMedia, jitter;
        private long lastPlayed = -1;
        private bool anchored;
        private double queuedDuration;
        public bool ResetRequired { get; private set; }
        private static double Now => Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency;

        private readonly record struct Entry(AudioData Audio, double Media, double Duration, double Arrival);

        public bool HasPending { get { lock (queue) return queue.Count > 0; } }

        public void Enqueue(AudioData audio, long sequence)
        {
            double duration = Duration(audio);
            if (duration <= 0 || duration > 200 || audio.frequency <= 0) return;
            double now = Now;
            bool timed = !string.IsNullOrEmpty(audio.captureSession) && audio.sampleCount > 0;
            double media = timed ? audio.captureSampleTime * 1000d / audio.frequency : sequence;
            lock (queue)
            {
                string incomingSession = timed ? audio.captureSession : "legacy";
                if (incomingSession != session)
                {
                    if (retiredSessions.Contains(incomingSession)) return;
                    if (session != null)
                    {
                        if (retiredSessions.Count >= 8) retiredSessions.Clear();
                        retiredSessions.Add(session);
                    }
                    session = incomingSession;
                    queue.Clear();
                    queuedDuration = 0;
                    lastPlayed = -1;
                    anchored = false;
                    ResetRequired = true;
                }
                if (sequence <= lastPlayed || queue.ContainsKey(sequence)) return;

                bool restart = !anchored || now - lastArrival > 500;
                if (!restart && media > lastMedia)
                {
                    double variation = Math.Abs((now - lastArrival) - (media - lastMedia));
                    jitter += (variation - jitter) / 16;
                    // Recover from a suspended client or sustained clock drift without replaying backlog.
                    double due = anchorLocal + media - anchorMedia;
                    restart = now - due > 200 || due - now > 300;
                }
                if (restart)
                {
                    queue.Clear();
                    queuedDuration = 0;
                    anchorMedia = media;
                    anchorLocal = now + Math.Clamp(40 + 4 * jitter, 40, 100);
                    anchored = true;
                    ResetRequired = true;
                }
                if (media >= lastMedia || restart)
                {
                    lastArrival = now;
                    lastMedia = media;
                }
                queue.Add(sequence, new Entry(audio, media, duration, now));
                queuedDuration += duration;
                while (queue.Count > 0 && (queuedDuration > 200 || now - queue.Values[0].Arrival > 200))
                    RemoveFirst();
            }
        }

        public bool TryTake(bool playing, out AudioData audio, out int waitMs, out bool reset)
        {
            lock (queue)
            {
                audio = null;
                waitMs = 0;
                reset = ResetRequired;
                ResetRequired = false;
                while (queue.Count > 0)
                {
                    Entry entry = queue.Values[0];
                    double now = Now;
                    double due = anchorLocal + entry.Media - anchorMedia;
                    if (now - due > 100 || now - entry.Arrival > 200)
                    {
                        lastPlayed = queue.Keys[0];
                        RemoveFirst();
                        continue;
                    }
                    // A short hardware lookahead avoids an underrun at every packet boundary.
                    double remaining = due - now - (playing ? 20 : 0);
                    if (remaining > 0)
                    {
                        waitMs = Math.Clamp((int)Math.Ceiling(remaining), 1, 10);
                        return false;
                    }
                    lastPlayed = queue.Keys[0];
                    audio = entry.Audio;
                    RemoveFirst();
                    return true;
                }
                return false;
            }
        }

        private void RemoveFirst()
        {
            queuedDuration -= queue.Values[0].Duration;
            queue.RemoveAt(0);
        }

        public void Clear()
        {
            lock (queue) { queue.Clear(); queuedDuration = 0; anchored = false; }
        }

        private static double Duration(AudioData audio)
        {
            if (audio == null || audio.captureSampleTime < 0) return 0;
            if (audio.codec != OpusCodec._Name)
                return audio.sampleCount > 0 && audio.frequency > 0 ? audio.sampleCount * 1000d / audio.frequency : 100;
            int offset = 0, frames = 0;
            while (audio.data != null && offset + 4 <= audio.data.Length)
            {
                int length = BitConverter.ToInt32(audio.data, offset);
                offset += 4;
                if (length <= 0 || length > audio.data.Length - offset) return 0;
                offset += length;
                if (++frames > 20) return 0;
            }
            if (offset != audio.data?.Length) return 0;
            double duration = frames * 10;
            if (audio.sampleCount > 0 && Math.Abs(duration - audio.sampleCount * 1000d / audio.frequency) > 0.1) return 0;
            return duration;
        }
    }
}
