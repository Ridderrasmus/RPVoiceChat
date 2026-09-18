using System.Diagnostics;
#if VOICE_DIAGNOSTICS
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Vintagestory.API.Common;
#endif

namespace RPVoiceChat
{
    // Conditional calls (including their arguments) disappear from normal builds.
    internal static class VoiceDiagnostics
    {
#if VOICE_DIAGNOSTICS
        private static readonly ConcurrentDictionary<string, long> counters = new();
        private static readonly ConcurrentDictionary<string, long> peaks = new();
        private static readonly object lifecycle = new();
        private static Timer timer;
        private static int users;
        private static ILogger logger;
#endif
        [Conditional("VOICE_DIAGNOSTICS")]
        public static void Count(string name)
        {
#if VOICE_DIAGNOSTICS
            counters.AddOrUpdate(name, 1, (_, value) => value + 1);
#endif
        }

        [Conditional("VOICE_DIAGNOSTICS")]
        public static void Peak(string name, double value)
        {
#if VOICE_DIAGNOSTICS
            long rounded = (long)Math.Ceiling(value);
            peaks.AddOrUpdate(name, rounded, (_, previous) => Math.Max(previous, rounded));
#endif
        }

        [Conditional("VOICE_DIAGNOSTICS")]
        public static void Start(Vintagestory.API.Common.ILogger log)
        {
#if VOICE_DIAGNOSTICS
            lock (lifecycle)
            {
                if (users++ != 0) return;
                logger = log;
                counters.Clear();
                peaks.Clear();
                log.Notification("[RPVC-Diagnostics] Diagnostic build enabled. Cumulative counters and peak milliseconds every 10s; no audio or player identities recorded.");
                timer = new Timer(_ => Report(), null, 10000, 10000);
            }
#endif
        }

        [Conditional("VOICE_DIAGNOSTICS")]
        public static void Stop()
        {
#if VOICE_DIAGNOSTICS
            lock (lifecycle)
            {
                if (users == 0 || --users != 0) return;
                timer?.Dispose();
                timer = null;
                Report();
            }
#endif
        }

#if VOICE_DIAGNOSTICS
        private static void Report()
        {
            try
            {
                if (counters.IsEmpty && peaks.IsEmpty) return;
                string counts = string.Join(" ", counters.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
                string maxima = string.Join(" ", peaks.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
                logger?.Notification($"[RPVC-Diagnostics] totals {counts}; peaks {maxima}");
            }
            catch { /* Diagnostics must never interrupt voice or shutdown. */ }
        }
#endif
    }
}
