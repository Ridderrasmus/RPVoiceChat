using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using RPVoiceChat.Util;

namespace RPVoiceChat.Audio.Input
{
    public sealed class RadioHlsStreamCapture : IDisposable
    {
        public const int SampleRate = 48000;
        public const int Channels = 1;
        public const int BytesPerSample = sizeof(short);
        public const int FrameSamples = SampleRate / 100;
        public const int FrameBytes = FrameSamples * BytesPerSample;

        private Process ffmpegProcess;
        private Thread readerThread;
        private CancellationTokenSource cancellation;
        private readonly object sync = new();

        public bool IsRunning { get; private set; }
        public string LastError { get; private set; } = "";

        public event Action<short[]> OnPcmFrame;

        public bool TryStart(string streamUrl)
        {
            Stop();

            if (string.IsNullOrWhiteSpace(streamUrl))
            {
                LastError = "Empty HLS URL.";
                return false;
            }

            if (!Uri.TryCreate(streamUrl.Trim(), UriKind.Absolute, out Uri uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                LastError = "HLS URL must start with http:// or https://.";
                return false;
            }

            string ffmpegExecutable = FfmpegLocator.ResolveExecutable();
            if (ffmpegExecutable == null)
            {
                LastError = FfmpegLocator.GetMissingMessage();
                return false;
            }

            try
            {
                cancellation = new CancellationTokenSource();
                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegExecutable,
                    Arguments = BuildFfmpegArguments(uri.AbsoluteUri),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                ffmpegProcess = Process.Start(startInfo);
                if (ffmpegProcess == null)
                {
                    LastError = "Failed to start FFmpeg.";
                    return false;
                }

                readerThread = new Thread(ReadLoop)
                {
                    IsBackground = true,
                    Name = "RPVC-HLS"
                };
                readerThread.Start(cancellation.Token);
                IsRunning = true;
                LastError = "";
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Stop();
                return false;
            }
        }

        public void Stop()
        {
            lock (sync)
            {
                try
                {
                    cancellation?.Cancel();
                }
                catch
                {
                    // Ignore cancellation failures during shutdown.
                }

                if (ffmpegProcess != null)
                {
                    try
                    {
                        if (!ffmpegProcess.HasExited)
                        {
                            ffmpegProcess.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // Ignore process cleanup failures.
                    }

                    ffmpegProcess.Dispose();
                    ffmpegProcess = null;
                }

                readerThread = null;
                cancellation?.Dispose();
                cancellation = null;
                IsRunning = false;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ReadLoop(object tokenObj)
        {
            if (tokenObj is not CancellationToken token || ffmpegProcess?.StandardOutput?.BaseStream == null)
            {
                return;
            }

            var frameBuffer = new byte[FrameBytes];
            int frameOffset = 0;
            var readBuffer = new byte[4096];

            try
            {
                Stream stdout = ffmpegProcess.StandardOutput.BaseStream;
                while (!token.IsCancellationRequested && !ffmpegProcess.HasExited)
                {
                    int read = stdout.Read(readBuffer, 0, readBuffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    int sourceOffset = 0;
                    while (sourceOffset < read)
                    {
                        int copy = Math.Min(FrameBytes - frameOffset, read - sourceOffset);
                        Buffer.BlockCopy(readBuffer, sourceOffset, frameBuffer, frameOffset, copy);
                        frameOffset += copy;
                        sourceOffset += copy;

                        if (frameOffset < FrameBytes)
                        {
                            continue;
                        }

                        EmitFrame(frameBuffer);
                        frameOffset = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    LastError = ex.Message;
                    Logger.client.Warning($"[RadioHlsStreamCapture] Stream read failed: {ex.Message}");
                }
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void EmitFrame(byte[] frameBuffer)
        {
            var samples = new short[FrameSamples];
            var span = MemoryMarshal.Cast<byte, short>(frameBuffer.AsSpan(0, FrameBytes));
            span.CopyTo(samples);
            OnPcmFrame?.Invoke(samples);
        }

        private static string BuildFfmpegArguments(string streamUrl)
        {
            return $"-hide_banner -loglevel error -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i \"{streamUrl}\" -vn -ac 1 -ar {SampleRate} -f s16le pipe:1";
        }
    }
}
