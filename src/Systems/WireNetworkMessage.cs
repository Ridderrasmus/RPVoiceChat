using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Systems
{
    public enum WireRouteMode
    {
        All = 0,
        NamedEndpoint = 1
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WireNetworkMessage
    {
        public long NetworkUID;
        public string Message;
        public BlockPos SenderPos;
        /// <summary>UID of the player who sent. Lets other clients play animation/sound on the same block.</summary>
        public string SenderPlayerUID;
        public WireRouteMode RouteMode = WireRouteMode.All;
        public string TargetEndpointName;
        public BlockPos TargetPos;
    }
}
