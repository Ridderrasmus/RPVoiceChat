using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.Client.NoObf;

namespace RPVoiceChat
{
    static class SystemNetworkProcessPatch
    {
        public static event Func<int, Packet_CustomPacket, bool> OnProcessInBackground;

        private const int _customPacketId = 55;

        public static void Patch(Harmony harmony)
        {
            var OriginalMethod = AccessTools.Method(typeof(SystemNetworkProcess), nameof(ProcessInBackground));
            var PostfixMethod = AccessTools.Method(typeof(SystemNetworkProcessPatch), nameof(ProcessInBackground));
            harmony.Patch(OriginalMethod, postfix: new HarmonyMethod(PostfixMethod));
        }

        private static FieldInfo packetIdField = AccessTools.Field(typeof(Packet_Server), "Id");
        private static FieldInfo customPacketField = AccessTools.Field(typeof(Packet_Server), "CustomPacket");
        private static FieldInfo channelIdField = AccessTools.Field(typeof(Packet_CustomPacket), "ChannelId");

        public static void ProcessInBackground(ref bool __result, Packet_Server packet)
        {
            var id = (int)packetIdField.GetValue(packet);
            if (id != _customPacketId) return;

            var customPacket = (Packet_CustomPacket)customPacketField.GetValue(packet);
            var channelId = (int)channelIdField.GetValue(customPacket);
            bool processed = OnProcessInBackground?.Invoke(channelId, customPacket) ?? false;
            if (processed) __result = true;
        }
    }
}
