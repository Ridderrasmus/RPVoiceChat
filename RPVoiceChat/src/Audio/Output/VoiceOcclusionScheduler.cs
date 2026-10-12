using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Client;

namespace RPVoiceChat.Audio
{
    /// <summary>Shares a small main-thread budget across all voice occlusion queries.</summary>
    public sealed class VoiceOcclusionScheduler : IDisposable
    {
        private readonly ICoreClientAPI api;
        private readonly Queue<(Func<bool> Step, Action Complete)> pending = new();
        private readonly long tick;
        private bool disposed;
        private long lastError;

        public VoiceOcclusionScheduler(ICoreClientAPI api)
        {
            this.api = api;
            tick = api.Event.RegisterGameTickListener(Update, 20);
        }

        public bool TryEnqueue(Func<bool> step, Action complete)
        {
            lock (pending)
            {
                if (disposed || pending.Count >= 128) return false;
                pending.Enqueue((step, complete));
                return true;
            }
        }

        private void Update(float dt)
        {
            long start = Stopwatch.GetTimestamp();
            // Each step traces one block. Check the budget between steps; third-party
            // Sound Physics calls are indivisible, so they can exceed this soft limit.
            for (int i = 0; i < 8 && Stopwatch.GetElapsedTime(start).TotalMilliseconds < 2; i++)
            {
                (Func<bool> Step, Action Complete) query;
                lock (pending)
                {
                    if (disposed || pending.Count == 0) return;
                    query = pending.Dequeue();
                }
                bool more = false;
                try { more = query.Step(); }
                catch (Exception e)
                {
                    long now = Environment.TickCount64;
                    if (now - lastError >= 5000)
                    {
                        lastError = now;
                        api.Logger.Warning("[RPVoiceChat] Occlusion query failed: " + e.Message);
                    }
                }
                if (!more || !TryEnqueue(query.Step, query.Complete)) query.Complete();
            }
        }

        public void Dispose()
        {
            api.Event.UnregisterGameTickListener(tick);
            lock (pending)
            {
                disposed = true;
                while (pending.Count > 0) pending.Dequeue().Complete();
            }
        }
    }
}
