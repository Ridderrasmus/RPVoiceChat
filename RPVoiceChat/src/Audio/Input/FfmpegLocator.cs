using System;
using System.IO;
using System.Threading.Tasks;
using RPVoiceChat.Util;
using Xabe.FFmpeg.Downloader;

namespace RPVoiceChat.Audio.Input
{
    /// <summary>
    /// Resolves the FFmpeg CLI used for HLS program decode.
    /// Order: manual bundle → auto-download cache → PATH.
    /// Auto-download uses Xabe.FFmpeg.Downloader (Windows / Linux / macOS).
    /// </summary>
    public static class FfmpegLocator
    {
        private static string modFolder;
        private static readonly object ensureLock = new();
        private static Task ensureTask;
        private static string lastFailureMessage;

        public static void SetModFolder(string folderPath)
        {
            modFolder = folderPath;
        }

        public static string ResolveExecutable()
        {
            return ResolveBundledExecutable()
                ?? ResolveAutoInstalledExecutable()
                ?? ResolveFromPath();
        }

        public static bool IsAvailable()
        {
            return !string.IsNullOrWhiteSpace(ResolveExecutable());
        }

        /// <summary>
        /// Starts a background download if FFmpeg is not already available.
        /// Safe to call multiple times.
        /// </summary>
        public static void BeginEnsureAvailable()
        {
            _ = EnsureAvailableAsync();
        }

        /// <summary>
        /// Ensures FFmpeg is available, downloading for the current OS if needed.
        /// </summary>
        public static Task EnsureAvailableAsync()
        {
            lock (ensureLock)
            {
                if (IsAvailable())
                {
                    return Task.CompletedTask;
                }

                if (ensureTask == null
                    || ensureTask.IsFaulted
                    || (ensureTask.IsCompleted && !IsAvailable()))
                {
                    ensureTask = DownloadIfNeededAsync();
                }

                return ensureTask;
            }
        }

        /// <summary>
        /// Blocks until FFmpeg is ready or the timeout elapses.
        /// </summary>
        public static bool TryEnsureAvailable(TimeSpan timeout)
        {
            if (IsAvailable())
            {
                return true;
            }

            try
            {
                Task task = EnsureAvailableAsync();
                if (!task.Wait(timeout))
                {
                    lastFailureMessage = "FFmpeg download timed out.";
                    return false;
                }

                return IsAvailable();
            }
            catch (Exception ex)
            {
                lastFailureMessage = UnwrapMessage(ex);
                return IsAvailable();
            }
        }

        public static string GetMissingMessage()
        {
            if (!string.IsNullOrWhiteSpace(lastFailureMessage))
            {
                return $"FFmpeg not found ({lastFailureMessage}). " +
                       "Install FFmpeg on the server PATH, place a binary in Lib/ffmpeg/{win|linux|osx}/, " +
                       "or allow the server to download it into Lib/ffmpeg/auto/.";
            }

            return "FFmpeg not found. It will be downloaded automatically into Lib/ffmpeg/auto/ when the server has internet access. " +
                   "Alternatively install FFmpeg on PATH or place a binary in Lib/ffmpeg/{win|linux|osx}/.";
        }

        private static async Task DownloadIfNeededAsync()
        {
            if (IsAvailable())
            {
                return;
            }

            string installDir = GetAutoInstallDirectory();
            if (string.IsNullOrWhiteSpace(installDir))
            {
                lastFailureMessage = "mod folder unknown";
                return;
            }

            try
            {
                Directory.CreateDirectory(installDir);
                Logger.server?.Notification($"Downloading FFmpeg for this platform into {installDir} …");
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, installDir).ConfigureAwait(false);
                MakeUnixExecutable(ResolveAutoInstalledExecutable());

                if (IsAvailable())
                {
                    lastFailureMessage = null;
                    Logger.server?.Notification($"FFmpeg ready: {ResolveExecutable()}");
                }
                else
                {
                    lastFailureMessage = "download finished but ffmpeg executable was not found";
                    Logger.server?.Error(GetMissingMessage());
                }
            }
            catch (Exception ex)
            {
                lastFailureMessage = UnwrapMessage(ex);
                Logger.server?.Error($"FFmpeg download failed: {lastFailureMessage}");
            }
        }

        private static string GetAutoInstallDirectory()
        {
            if (string.IsNullOrWhiteSpace(modFolder))
            {
                return null;
            }

            return Path.Combine(modFolder, "Lib", "ffmpeg", "auto");
        }

        private static string ResolveBundledExecutable()
        {
            if (string.IsNullOrWhiteSpace(modFolder))
            {
                return null;
            }

            string platformFolder = GetPlatformFolderName();
            if (platformFolder == null)
            {
                return null;
            }

            string executableName = GetExecutableFileName();
            string[] candidates =
            {
                Path.Combine(modFolder, "Lib", "ffmpeg", platformFolder, executableName),
                Path.Combine(modFolder, "lib", "ffmpeg", platformFolder, executableName)
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string ResolveAutoInstalledExecutable()
        {
            string installDir = GetAutoInstallDirectory();
            if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            {
                return null;
            }

            string executableName = GetExecutableFileName();
            string direct = Path.Combine(installDir, executableName);
            if (File.Exists(direct))
            {
                return direct;
            }

            try
            {
                foreach (string candidate in Directory.EnumerateFiles(installDir, executableName, SearchOption.AllDirectories))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore transient IO errors while resolving.
            }

            return null;
        }

        private static string ResolveFromPath()
        {
            if (OperatingSystem.IsWindows())
            {
                return TryResolveFromPath("ffmpeg.exe") ?? TryResolveFromPath("ffmpeg");
            }

            return TryResolveFromPath("ffmpeg");
        }

        private static string GetPlatformFolderName()
        {
            if (OperatingSystem.IsWindows())
            {
                return "win";
            }

            if (OperatingSystem.IsLinux())
            {
                return "linux";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "osx";
            }

            return null;
        }

        private static string GetExecutableFileName()
        {
            return OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        }

        private static string TryResolveFromPath(string executable)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathEnv))
            {
                return null;
            }

            foreach (string folder in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(folder))
                {
                    continue;
                }

                try
                {
                    string candidate = Path.Combine(folder.Trim(), executable);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Ignore invalid PATH entries.
                }
            }

            return null;
        }

        private static void MakeUnixExecutable(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || OperatingSystem.IsWindows() || !File.Exists(path))
            {
                return;
            }

            try
            {
                UnixFileMode mode = File.GetUnixFileMode(path);
                mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                File.SetUnixFileMode(path, mode);
            }
            catch (Exception ex)
            {
                Logger.server?.Warning($"Could not set execute bit on FFmpeg: {ex.Message}");
            }
        }

        private static string UnwrapMessage(Exception ex)
        {
            if (ex is AggregateException aggregate && aggregate.InnerException != null)
            {
                return aggregate.InnerException.Message;
            }

            return ex?.Message ?? "unknown error";
        }
    }
}
