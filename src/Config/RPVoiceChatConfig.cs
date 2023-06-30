using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace rpvoicechat
{
    public class RPVoiceChatConfig
    {
        public Dictionary<string, string> Settings = new Dictionary<string, string>()
        {
            {"port", "52525" },
            {"bufferSize", ""}
        };


        public RPVoiceChatConfig() { }

        public RPVoiceChatConfig(RPVoiceChatConfig previousConfig)
        {
            Settings = previousConfig.Settings;
        }
    }
}
