using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Systems;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntitySpeaker : BEWireNode, IWireTypedNode, ITelephoneVoiceEndpoint
    {
        protected override int MaxConnections => 1;
        public override bool IsActiveEndpoint => true;
        public WireNodeKind WireNodeKind => WireNodeKind.Telephone;
        public int VoiceEmissionRangeBlocks => ServerConfigManager.SpeakerAudibleDistance;
    }
}
