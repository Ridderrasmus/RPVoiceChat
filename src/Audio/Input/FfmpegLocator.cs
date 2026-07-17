using System;
using System.IO;
using RPVoiceChat.Util;

namespace RPVoiceChat.Audio.Input
{
    public static class FfmpegLocator
    {
        private static string modFolder;

        public static void SetModFolder(string folderPath)
        {
            modFolder = folderPath;
        }

        public static string ResolveExecutable()
        {
            string bundled = ResolveBundledExecutable();
            if (!string.IsNullOrWhiteSpace(bundled))
            {
                return bundled;
            }

            if (OperatingSystem.IsWindows())
            {
                return TryResolveFromPath("ffmpeg.exe") ?? TryResolveFromPath("ffmpeg");
            }

            return TryResolveFromPath("ffmpeg");
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

            string executableName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
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

        public static bool IsAvailable()
        {
            return !string.IsNullOrWhiteSpace(ResolveExecutable());
        }

        public static string GetMissingMessage()
        {
            string platformFolder = GetPlatformFolderName() ?? "platform";
            string executableName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            return $"FFmpeg not found. Place {executableName} in Lib/ffmpeg/{platformFolder}/ inside the mod folder, or install FFmpeg on the server PATH.";
        }
    }
}
