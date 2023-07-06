using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace rpvoicechat
{
    public class ModConfig
    {
        
        public static ModConfig Loaded { get; set; } = new ModConfig();

        // Server relevant variables
        public int ServerPort { get; set; } = 52525;
        public bool UseUPnP { get; set; } = true;
        public int ShoutingDistance { get; set; } = 25;
        public int TalkingDistance { get; set; } = 15;
        public int WhisperingDistance { get; set; } = 5;

        // Client relevant variables
        public bool IsPushToTalkEnabled { get; set; } = false;
        public int InputThreshold { get; set; } = 20;
    }
}
