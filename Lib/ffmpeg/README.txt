FFmpeg for radio HLS (dedicated server)
=======================================

RPVoiceChat needs the FFmpeg CLI to decode HLS program streams on the
dedicated server. Resolution order:

  1. Manual bundle (optional)
       Lib/ffmpeg/win/ffmpeg.exe
       Lib/ffmpeg/linux/ffmpeg
       Lib/ffmpeg/osx/ffmpeg

  2. Auto-download cache (preferred default)
       Lib/ffmpeg/auto/
     Downloaded on first server start (or first on-air) via the
     Xabe.FFmpeg.Downloader NuGet package for the current OS
     (Windows, Linux, or macOS). Requires outbound internet once.

  3. System PATH (`ffmpeg` / `ffmpeg.exe`)

Manual static builds: https://ffmpeg.org/download.html
License: FFmpeg is LGPL/GPL — see https://ffmpeg.org/legal.html
