using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntitySwitchboard : BEWireNode, IWireTypedNode, ISwitchboardNode
    {
        public WireNodeKind WireNodeKind => WireNodeKind.Switchboard;
        public float PowerPercent { get; private set; }

        protected override int MaxConnections => 4;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 250);
                TryDiscoverNetwork();
            }
        }

        public void TryDiscoverNetwork()
        {
            if (Block?.Variant == null || !Block.Variant.TryGetValue("side", out string sideStr)) return;
            BlockFacing frontFace = BlockFacing.FromCode(sideStr);
            if (frontFace == null) return;
            BlockFacing connectorFace = frontFace.Opposite;

            var mechBase = GetBehavior<BEBehaviorMPBase>();
            mechBase?.CreateJoinAndDiscoverNetwork(connectorFace);
        }

        private void OnServerTick(float dt)
        {
            var consumer = GetBehavior<BEBehaviorMPConsumer>();
            float newPower = 0f;
            if (consumer != null)
            {
                newPower = GameMath.Clamp(consumer.TrueSpeed, 0f, 1f);
            }

            WireNetworkKind currentKind = ResolveEffectiveNetworkKind();
            float minPower = WireNetworkTypeRules.GetRequirements(currentKind).MinPowerPercent;
            bool powerChanged = System.Math.Abs(newPower - PowerPercent) > 0.005f;
            bool stateChanged = (newPower >= minPower) != (PowerPercent >= minPower);
            if (!powerChanged && !stateChanged) return;

            PowerPercent = newPower;
            MarkDirty();
            if (NetworkUID != 0)
            {
                WireNetworkHandler.RebuildNetworkState(NetworkUID);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("switchboardPowerPercent", PowerPercent);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            PowerPercent = tree.GetFloat("switchboardPowerPercent", 0f);
            if (NetworkUID != 0)
            {
                WireNetworkHandler.RebuildNetworkState(NetworkUID);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            WireNetworkKind currentKind = ResolveEffectiveNetworkKind();

            dsc.AppendLine(UIUtils.I18n("Switchboard.Power", (int)(PowerPercent * 100)));
            dsc.AppendLine(HasSufficientPowerFor(currentKind)
                ? UIUtils.I18n("Switchboard.PowerReady")
                : UIUtils.I18n("Switchboard.PowerLow"));
        }

        public bool HasSufficientPowerFor(WireNetworkKind networkKind)
        {
            WireNetworkRequirements requirements = WireNetworkTypeRules.GetRequirements(networkKind);
            return PowerPercent >= requirements.MinPowerPercent;
        }

        private WireNetworkKind ResolveEffectiveNetworkKind()
        {
            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network == null || network.CurrentType == WireNetworkKind.None || network.CurrentType == WireNetworkKind.Mixed)
            {
                return WireNetworkKind.Telegraph;
            }

            return network.CurrentType;
        }
    }
}
