using OpenTK.Audio.OpenAL;
using System;

namespace RPVoiceChat.Utils
{
    public static class AudioUtils
    {
        public static int ChannelsPerFormat(ALFormat format)
        {
            switch (format)
            {
                case ALFormat.Mono16:
                    return 1;
                case ALFormat.MultiQuad16Ext:
                    return 4;
                default:
                    throw new NotSupportedException($"Format {format} is not supported for capture");
            }
        }
    }
}