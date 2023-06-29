using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace rpvoicechat
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ConnectionPacket
    {
        public string playerUid;
        public string serverIp;
    }
}
