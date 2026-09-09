using RPVoiceChat.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace RPVoiceChat.Util
{
    public static class NetworkUtils
    {
        public static IPAddress ParseIP(string ipString)
        {
            if (ipString == null) throw new ArgumentNullException("IP string can not be null");
            string[] addressParts = ipString.Split(':');

            // [IPv4], [IPv6], [IPv6.IPv4], [IPv4]:Port, [IPv6]:Port or [IPv6.IPv4]
            int endOfAddressIndex = ipString.IndexOf(']');
            if (ipString.StartsWith("[") && endOfAddressIndex != -1)
                return IPAddress.Parse(ipString.Substring(1, endOfAddressIndex - 1));

            // IPv4 or IPv4:Port
            if (addressParts.Length == 1 || addressParts.Length == 2)
                return IPAddress.Parse(addressParts[0]);

            // IPv6
            try
            {
                return IPAddress.Parse(ipString);
            }
            catch (FormatException) { }

            // IPv6:Port
            try
            {
                var listAddressParts = new List<string>(addressParts);
                listAddressParts.Remove(listAddressParts.Last());
                string newIpString = string.Join(":", listAddressParts);
                return IPAddress.Parse(newIpString);
            }
            catch (FormatException) { }

            throw new ArgumentException($"IP string \"{ipString}\" is not a valid IP address");
        }

        public static bool IsInternalNetwork(string ip)
        {
            return IsInternalNetwork(IPAddress.Parse(ip));
        }

        public static bool IsInternalNetwork(IPAddress ip)
        {
            byte[] ipParts = ip.GetAddressBytes();

            if (ipParts[0] == 10 ||
               (ipParts[0] == 192 && ipParts[1] == 168) ||
               (ipParts[0] == 172 && (ipParts[1] >= 16 && ipParts[1] <= 31)) ||
               (ipParts[0] == 25 || ipParts[0] == 26) ||
               (ipParts[0] == 127 && ipParts[1] == 0 && ipParts[2] == 0 && ipParts[3] == 1))
                return true;

            return false;
        }

        public static IPEndPoint GetEndPoint(ConnectionInfo connectionInfo)
        {
            var address = IPAddress.Parse(connectionInfo.Address);
            var endpoint = new IPEndPoint(address, connectionInfo.Port);

            return endpoint;
        }

        public static string GetPublicIP()
        {
            string publicIPString = new HttpClient().GetStringAsync("https://ipinfo.io/ip").GetAwaiter().GetResult();

            return publicIPString;
        }

        public static bool AssertEqual(IPEndPoint firstEndPoint, IPEndPoint secondEndPoint)
        {
            bool isSameAddress = firstEndPoint.Address.MapToIPv4().ToString() == secondEndPoint.Address.MapToIPv4().ToString();
            bool isSamePort = firstEndPoint.Port == secondEndPoint.Port;
            return isSameAddress && isSamePort;
        }
    }
}
