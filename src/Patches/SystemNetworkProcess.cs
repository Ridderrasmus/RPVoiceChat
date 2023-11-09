using HarmonyLib;
using System;
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

        public static void ProcessInBackground(ref bool __result, Packet_Server packet)
        {
            var id = (int)AccessTools.Field(typeof(Packet_Server), "Id").GetValue(packet);
            if (id != _customPacketId) return;

            var customPacket = (Packet_CustomPacket)AccessTools.Field(typeof(Packet_Server), "CustomPacket").GetValue(packet);
            var channelId = (int)AccessTools.Field(typeof(Packet_CustomPacket), "ChannelId").GetValue(customPacket);
            bool processed = OnProcessInBackground?.Invoke(channelId, customPacket) ?? false;
            if (processed) __result = true;
        }
    }
}
