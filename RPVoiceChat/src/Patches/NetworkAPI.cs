using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.Server;

namespace RPVoiceChat
{
    /// <summary>
    /// Patches <b>server's</b> NetworkAPI to allow skipping handling of packets over certain channels
    /// </summary>
    static class NetworkAPIPatch
    {
        public static event Func<int, bool> OnHandleCustomPacket;

        public static void Patch(Harmony harmony)
        {
            var OriginalMethod = AccessTools.Method(typeof(NetworkAPI), nameof(HandleCustomPacket));
            var PrefixMethod = AccessTools.Method(typeof(NetworkAPIPatch), nameof(HandleCustomPacket));
            harmony.Patch(OriginalMethod, prefix: new HarmonyMethod(PrefixMethod));
        }

        private static FieldInfo CustomPacketField = AccessTools.Field(typeof(Packet_Client), "CustomPacket");
        private static FieldInfo ChannelIdField = AccessTools.Field(typeof(Packet_CustomPacket), "ChannelId");

        public static bool HandleCustomPacket(Packet_Client packet)
        {
            var customPacket = (Packet_CustomPacket)CustomPacketField.GetValue(packet);
            var channelId = (int)ChannelIdField.GetValue(customPacket);
            bool isAlreadyHandled = OnHandleCustomPacket?.Invoke(channelId) ?? false;
            bool shouldHandle = !isAlreadyHandled;
            return shouldHandle;
        }
    }
}
