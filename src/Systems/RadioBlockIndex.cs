using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Systems
{
    public static class RadioBlockIndex
    {
        private static readonly HashSet<BlockPos> emitterPositions = new();
        private static readonly HashSet<BlockPos> microphonePositions = new();
        private static readonly HashSet<BlockPos> receiverPositions = new();
        private static readonly HashSet<BlockPos> mixingConsolePositions = new();

        public static void RegisterEmitter(BlockPos pos)
        {
            if (pos != null)
            {
                emitterPositions.Add(pos.Copy());
            }
        }

        public static void UnregisterEmitter(BlockPos pos)
        {
            if (pos != null)
            {
                emitterPositions.Remove(pos);
            }
        }

        public static void RegisterMicrophone(BlockPos pos)
        {
            if (pos != null)
            {
                microphonePositions.Add(pos.Copy());
            }
        }

        public static void UnregisterMicrophone(BlockPos pos)
        {
            if (pos != null)
            {
                microphonePositions.Remove(pos);
            }
        }

        public static void RegisterReceiver(BlockPos pos)
        {
            if (pos != null)
            {
                receiverPositions.Add(pos.Copy());
            }
        }

        public static void UnregisterReceiver(BlockPos pos)
        {
            if (pos != null)
            {
                receiverPositions.Remove(pos);
            }
        }

        public static IEnumerable<BlockEntityRadioEmitter> GetLoadedEmitters(IWorldAccessor world)
        {
            if (world?.BlockAccessor == null)
            {
                yield break;
            }

            foreach (BlockPos pos in emitterPositions.ToArray())
            {
                if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityRadioEmitter emitter)
                {
                    yield return emitter;
                }
                else
                {
                    emitterPositions.Remove(pos);
                }
            }
        }

        public static IEnumerable<BlockEntityRadioMicrophone> GetLoadedMicrophones(IWorldAccessor world)
        {
            if (world?.BlockAccessor == null)
            {
                yield break;
            }

            foreach (BlockPos pos in microphonePositions.ToArray())
            {
                if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityRadioMicrophone microphone)
                {
                    yield return microphone;
                }
                else
                {
                    microphonePositions.Remove(pos);
                }
            }
        }

        public static void RegisterMixingConsole(BlockPos pos)
        {
            if (pos != null)
            {
                mixingConsolePositions.Add(pos.Copy());
            }
        }

        public static void UnregisterMixingConsole(BlockPos pos)
        {
            if (pos != null)
            {
                mixingConsolePositions.Remove(pos);
            }
        }

        public static IEnumerable<BlockEntityRadioMixingConsole> GetLoadedMixingConsoles(IWorldAccessor world)
        {
            if (world?.BlockAccessor == null)
            {
                yield break;
            }

            foreach (BlockPos pos in mixingConsolePositions.ToArray())
            {
                if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityRadioMixingConsole mixingConsole)
                {
                    yield return mixingConsole;
                }
                else
                {
                    mixingConsolePositions.Remove(pos);
                }
            }
        }

        public static IEnumerable<BlockEntityRadioReceiver> GetLoadedReceivers(IWorldAccessor world)
        {
            if (world?.BlockAccessor == null)
            {
                yield break;
            }

            foreach (BlockPos pos in receiverPositions.ToArray())
            {
                if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityRadioReceiver receiver)
                {
                    yield return receiver;
                }
                else
                {
                    receiverPositions.Remove(pos);
                }
            }
        }
    }
}
