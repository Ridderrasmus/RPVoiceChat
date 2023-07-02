using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace rpvoicechat
{
    public class RPModSettings
    {
        public static bool PushToTalkEnabled { get; internal set; }
        public static bool IsMuted { get; internal set; }
        public static int InputThreshold { get; internal set; }

        public static int serverPort { get; internal set; } = 52525;
        public static string CurrentInputDevice { get; internal set; } = "0";
    }
}
