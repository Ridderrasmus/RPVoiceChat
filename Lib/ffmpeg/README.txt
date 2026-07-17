Bundled FFmpeg (optional)
=========================

RPVoiceChat can use a FFmpeg binary shipped inside this mod folder so server
admins do not need a system-wide install.

Place the official FFmpeg build for your server OS here:

  win/ffmpeg.exe     Windows dedicated server
  linux/ffmpeg       Linux dedicated server
  osx/ffmpeg         macOS dedicated server

Download static builds from https://ffmpeg.org/download.html
(Windows: gyan.dev or BtbN builds; Linux: johnvansickle static builds, etc.)

License: FFmpeg is LGPL/GPL. When redistributing binaries, comply with the
FFmpeg license (source offer, attribution). See https://ffmpeg.org/legal.html

If no bundled binary is present, the mod falls back to `ffmpeg` on the server PATH.
