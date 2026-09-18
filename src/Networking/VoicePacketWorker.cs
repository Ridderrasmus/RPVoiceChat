using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    /// <summary>Serial voice dispatch with bounded backlog and no work on the producer thread.</summary>
    internal sealed class VoicePacketWorker<T> : IDisposable
    {
        private readonly Channel<(T Packet, long Arrived)> queue = Channel.CreateBounded<(T, long)>(
            new BoundedChannelOptions(256)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest,
                AllowSynchronousContinuations = false
            }
#if VOICE_DIAGNOSTICS
            , _ => VoiceDiagnostics.Count("native-overflow-drop")
#endif
            );
        private volatile bool disposed;

        public VoicePacketWorker(Action<T> handle, Action<Exception> reportError)
        {
            _ = Task.Run(async () =>
            {
                long lastError = 0;
                await foreach (var entry in queue.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    if (disposed) continue;
                    VoiceDiagnostics.Peak("native-queue-age-ms", Environment.TickCount64 - entry.Arrived);
                    if (Environment.TickCount64 - entry.Arrived > 200)
                    { VoiceDiagnostics.Count("native-expired-drop"); continue; }
                    try { handle(entry.Packet); }
                    catch (Exception e)
                    {
                        long now = Environment.TickCount64;
                        if (now - lastError >= 5000) { lastError = now; reportError(e); }
                    }
                }
            });
        }

        public void Enqueue(T packet)
        {
            if (!disposed) queue.Writer.TryWrite((packet, Environment.TickCount64));
        }

        public void Dispose()
        {
            disposed = true;
            queue.Writer.TryComplete();
        }
    }
}
