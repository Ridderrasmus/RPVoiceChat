using System.Collections.Generic;
using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public static class RadioConnectivityValidator
    {
        public static void ValidateNode(BEWireNode node)
        {
            if (node?.Api?.Side != EnumAppSide.Server)
            {
                return;
            }

            switch (node)
            {
                case BlockEntityRadioMixingConsole mixingConsole:
                    ValidateMixingConsole(mixingConsole);
                    break;
                case BlockEntityRadioMicrophone microphone:
                    ValidateMicrophone(microphone);
                    break;
            }
        }

        public static void ValidateMixingConsole(BlockEntityRadioMixingConsole mixingConsole)
        {
            if (mixingConsole == null || !mixingConsole.IsOnAir)
            {
                return;
            }

            if (mixingConsole.NetworkUID == 0 || !mixingConsole.HasWiredBroadcastPath())
            {
                mixingConsole.ClearOnAir();
            }
        }

        public static void ValidateMicrophone(BlockEntityRadioMicrophone microphone, ICoreServerAPI sapi = null)
        {
            if (microphone == null || !microphone.IsTransmitting)
            {
                return;
            }

            if (microphone.NetworkUID == 0 || !RadioWireNetworkHelper.IsRadioWiredNetwork(microphone))
            {
                microphone.ClearTransmission();
                return;
            }

            if (RadioWireNetworkHelper.HasOnAirMixingConsole(microphone))
            {
                return;
            }

            if (sapi == null)
            {
                return;
            }

            if (RadioWiredRouteBuilder.BuildRoutesForWiredNode(sapi, microphone).Count == 0)
            {
                microphone.ClearTransmission();
            }
        }

        public static void ValidateNodesInComponents(IEnumerable<BEWireNode> nodes)
        {
            if (nodes == null)
            {
                return;
            }

            foreach (BEWireNode node in nodes)
            {
                ValidateNode(node);
            }
        }
    }
}
