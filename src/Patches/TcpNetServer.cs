using System.Collections.Concurrent;
using HarmonyLib;
using RPVoiceChat.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Vintagestory.Common;
using Vintagestory.Server;

namespace RPVoiceChat
{
    /// <summary>
    /// Copies TCP messages received by server into own message queue for further processing from separate thread
    /// </summary>
    static class TcpNetServerPatch
    {
        private static readonly CodeInstruction anchor = new CodeInstruction(
            OpCodes.Callvirt,
            AccessTools.Method(typeof(ConcurrentQueue<NetIncomingMessage>), "Enqueue")
        );
        private static readonly List<CodeInstruction> patch = new List<CodeInstruction>() {
            // Pass first local variable (msg)
            new CodeInstruction(OpCodes.Ldloc_0),
            // Into OnReceivedMessage
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TcpNetServerPatch), nameof(OnReceivedMessage)))
        };
        private static Queue<NetIncomingMessage> messages = new Queue<NetIncomingMessage>();

        public static void Patch(Harmony harmony)
        {
            var OriginalMethod = AccessTools.Method(typeof(TcpNetServer), "ServerReceivedMessage");
            var TranspilerMethod = AccessTools.Method(typeof(TcpNetServerPatch), nameof(server_ReceivedMessage_Transpiler));
            harmony.Patch(OriginalMethod, transpiler: new HarmonyMethod(TranspilerMethod));
        }

        public static IEnumerable<CodeInstruction> server_ReceivedMessage_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var patchIndex = -1;
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                var codeInstr = codes[i];
                if (codeInstr.opcode != anchor.opcode) continue;
                if (codeInstr.operand != anchor.operand) continue;
                patchIndex = i;
                break;
            }

            if (patchIndex == -1)
            {
                Logger.server.Error("Couldn't find transpiler anchor for TcpNetServer patch");
                return instructions;
            }

            codes.InsertRange(patchIndex, patch);
            Logger.server.Notification("TcpNetServer was successfully patched");
            return codes.AsEnumerable();
        }

        private static void OnReceivedMessage(NetIncomingMessage msg)
        {
            lock (messages) messages.Enqueue(msg);
        }

        public static NetIncomingMessage ReadMessage()
        {
            lock (messages)
            {
                if (messages.Count == 0) return null;
                return messages.Dequeue();
            }
        }
    }
}
