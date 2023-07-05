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

        // Client relevant variables
        public bool IsPushToTalkEnabled { get; set; } = false;
        public int InputThreshold { get; set; } = 20;
    }
}
