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
        private double playedMediaEnd = double.NegativeInfinity;
        private bool needsPrime = true, primeAnchored, observedPlaying;
        private double primeDeadline, primeTargetMs;
        private bool anchored;
        private bool clockNeedsRebase;
        private const double MaxQueuedAudioMs = 200;
        // Separate suspension expiry from the amount of speech allowed in the queue.
        private const double MaxResidenceMs = 500;
        private double queuedDuration;
        public bool ResetRequired { get; private set; }
        private static double Now => Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency;

        private readonly record struct Entry(AudioData Audio, double Media, double Duration, double Arrival);

        public bool HasPending { get { lock (queue) return queue.Count > 0; } }

        public void Enqueue(AudioData audio, long sequence)
        {
            double duration = Duration(audio);
            if (duration <= 0 || duration > 200 || audio.frequency <= 0)
            { return; }
            double now = Now;
            bool timed = !string.IsNullOrEmpty(audio.captureSession) && audio.sampleCount > 0;
            double media = timed ? audio.captureSampleTime * 1000d / audio.frequency : sequence;
            long order = timed ? audio.captureSampleTime : sequence;
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
                    playedMediaEnd = double.NegativeInfinity;
                    jitter = 0;
                    anchored = false;
                    clockNeedsRebase = false;
                    needsPrime = true;
                    primeAnchored = false;
                    observedPlaying = false;
                    ResetRequired = true;
                }
                if (order <= lastPlayed || (timed && media < playedMediaEnd - 0.01) || queue.ContainsKey(order))
                { return; }
                if (timed) foreach (var queued in queue.Values)
                {
                    if (media < queued.Media + queued.Duration - 0.01 && media + duration > queued.Media + 0.01)
                    { return; }
                }

                if (anchored && media > lastMedia)
                {
                    double variation = Math.Abs((now - lastArrival) - (media - lastMedia));
                    // Speech pauses are not network jitter.
                    if (media - lastMedia <= MaxQueuedAudioMs)
                        jitter += (variation - jitter) / 16;
                }
                if (media >= lastMedia || !anchored)
                {
                    lastArrival = now;
                    lastMedia = media;
                }
                queue.Add(order, new Entry(audio, media, duration, now));
                queuedDuration += duration;
                while (queue.Count > 0 && queuedDuration > MaxQueuedAudioMs)
                {
                    lastPlayed = queue.Keys[0];
                    playedMediaEnd = queue.Values[0].Media + queue.Values[0].Duration;
                    RemoveFirst();
                    // Trim only the excess. Rebase on the surviving head when consumed;
                    // otherwise its old deadline can cause the entire remainder to be lost.
                    clockNeedsRebase = true;
                }
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
                if (reset) playing = false;
                if (!playing && observedPlaying && !reset)
                {
                    needsPrime = true;
                    primeAnchored = false;
                }
                observedPlaying = playing && !reset;
                bool discardedExpired = false;
                while (queue.Count > 0)
                {
                    Entry entry = queue.Values[0];
                    double now = Now;
                    if (now - entry.Arrival > MaxResidenceMs)
                    {
                        lastPlayed = queue.Keys[0];
                        playedMediaEnd = entry.Media + entry.Duration;
                        RemoveFirst();
                        discardedExpired = true;
                        clockNeedsRebase = true;
                        continue;
                    }
                    double due = anchorLocal + entry.Media - anchorMedia;
                    bool clockBehind = anchored && now - due > 100;
                    bool clockAhead = anchored && due - now > 300;
                    if (!anchored || clockNeedsRebase || clockBehind || clockAhead || (needsPrime && !primeAnchored))
                    {
                        // OpenAL keeps playing its existing audio. Recover the software
                        // clock around fresh retained speech instead of dropping it or
                        // stopping audio already queued on the device.
                        anchorMedia = entry.Media;
                        anchorLocal = now + (playing ? 40 : 0);
                        anchored = true;
                        clockNeedsRebase = false;
                        needsPrime = !playing;
                        due = anchorLocal;
                    }
                    if (needsPrime)
                    {
                        if (!primeAnchored)
                        {
                            primeTargetMs = Math.Clamp(40 + 4 * jitter, 40, 100);
                            primeDeadline = now + primeTargetMs;
                            primeAnchored = true;
                        }
                        // A restart owns one deadline, independent of incoming packets
                        // and of which head survives trimming. Buffered speech already
                        // supplies the cushion; do not wait another cushion on top of it.
                        bool buffered = queuedDuration >= primeTargetMs;
                        bool expired = now >= primeDeadline;
                        anchorMedia = entry.Media;
                        anchorLocal = buffered || expired ? now : primeDeadline;
                        due = anchorLocal;
                    }
                    // Once primed, keep two 20ms packets ahead in OpenAL. This is
                    // hardware lookahead, not extra delay added to each packet.
                    double remaining = due - now - (playing && !needsPrime ? 40 : 0);
                    if (remaining > 0)
                    {
                        waitMs = Math.Clamp((int)Math.Ceiling(remaining), 1, 10);
                        return false;
                    }
                    lastPlayed = queue.Keys[0];

                    playedMediaEnd = entry.Media + entry.Duration;
                    needsPrime = false;
                    audio = entry.Audio;
                    RemoveFirst();
                    return true;
                }
                if (discardedExpired)
                {
                    // A suspended client must wait for fresh speech.
                    anchored = false;
                    needsPrime = true;
                    primeAnchored = false;
                    observedPlaying = false;
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
            lock (queue)
            {
                queue.Clear(); queuedDuration = 0; anchored = false;
                needsPrime = true; primeAnchored = false; observedPlaying = false;
            }
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
