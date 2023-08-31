using ProtoBuf;

namespace rpvoicechat
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ConnectionInfo
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public string playeruid { get; set; }
    }
}
