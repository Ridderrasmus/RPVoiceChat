using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace RPVoiceChat.Utils
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
            } catch (FormatException) { }

            // IPv6:Port
            try
            {
                var listAddressParts = new List<string>(addressParts);
                listAddressParts.Remove(listAddressParts.Last());
                string newIpString = string.Join(":", listAddressParts);
                return IPAddress.Parse(newIpString);
            } catch (FormatException) { }

            throw new ArgumentException($"IP string \"{ipString}\" is not a valid IP address");
        }
    }
}
