#nullable enable

using System;
using System.Reflection;
using System.Text;
using RPVoiceChat;
using RPVoiceChat.GameContent.BlockEntityBehavior;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using RPVoiceChat.Util;

namespace RPVoiceChat.GameContent.BlockEntity
{
    /// <summary>
    /// Bell hammer: mechanical power consumer, plays animation then triggers adjacent carillon/church bell.
    /// At 25% power: ~1h in-game between strikes; at 100%: ~1s IRL. Right-click to enable/disable.
    /// </summary>
    public class BlockEntityBellHammer : Vintagestory.API.Common.BlockEntity
    {
        public const float MinPowerThreshold = 0.25f;
        /// <summary>At 25% power: interval between strikes (in-game hours).</summary>
        public const float IntervalGameHoursAtMinPower = 1f;
        /// <summary>At 100% power: interval in-game hours (≈ 1 second IRL if 1 day = 24 min).</summary>
        public const float IntervalGameHoursAtMaxPower = 1f / 60f;
        public const float AnimationToBellDelaySeconds = 0.4f;

        private bool _enabled = true;
        private double _nextStrikeTime;
        private bool _animationPlaying;
        private long _animationEndCallbackId = -1;

        private BEBehaviorAnimatable Animatable => GetBehavior<BEBehaviorAnimatable>();

        public bool Enabled => _enabled;
        public float PowerPercent { get; private set; }

        public BlockEntityBellHammer()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Client)
                Animatable?.InitializeAnimatorWithRotation("bellhammer");
            if (api.Side == EnumAppSide.Server)
            {
                (api as ICoreServerAPI)?.Event.RegisterGameTickListener(OnServerGameTick, 100);
                TryDiscoverNetwork();
            }
        }

        /// <summary>
        /// Forces the MPConsumer to discover the network in the connector direction (axle side).
        /// Without this, the consumer never joins the network and TrueSpeed stays 0.
        /// Called on init and when a neighbour changes (e.g. axle placed next to the hammer).
        /// </summary>
        public void TryDiscoverNetwork()
        {
            try
            {
                if (Block?.Variant == null || !Block.Variant.TryGetValue("side", out string sideStr)) return;
                BlockFacing frontFace = BlockFacing.FromCode(sideStr);
                if (frontFace == null) return;
                BlockFacing connectorFace = frontFace.Opposite;

                Type baseType = Type.GetType("Vintagestory.GameContent.Mechanics.BEBehaviorMPBase, VSSurvivalMod");
                if (baseType == null) return;
                var getBehaviorMethod = typeof(Vintagestory.API.Common.BlockEntity).GetMethod("GetBehavior").MakeGenericMethod(baseType);
                object beh = getBehaviorMethod.Invoke(this, null);
                if (beh == null) return;

                var prop = baseType.GetProperty("OutFacingForNetworkDiscovery", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    var setter = prop.GetSetMethod(nonPublic: true);
                    setter?.Invoke(beh, new object[] { connectorFace });
                }

                var discoverMethod = baseType.GetMethod("CreateJoinAndDiscoverNetwork", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(BlockFacing) }, null);
                discoverMethod?.Invoke(beh, new object[] { connectorFace });
            }
            catch
            {
                // Ignore if the mechanics mod is not present or API differs
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (Api?.Side == EnumAppSide.Server && _animationEndCallbackId >= 0)
                Api.World.UnregisterCallback(_animationEndCallbackId);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("enabled", _enabled);
            tree.SetFloat("powerPercent", PowerPercent);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _enabled = tree.GetBool("enabled", true);
            PowerPercent = tree.GetFloat("powerPercent", 0f);
        }

        private void OnServerGameTick(float dt)
        {
            if (!_enabled || Block == null) return;

            float power = GetMechanicalPowerPercent();
            PowerPercent = power;

            if (power < MinPowerThreshold) return;

            BlockPos? bellPos = GetAdjacentBellPosition();
            if (bellPos == null) return;

            double nowGameHours = Api.World.Calendar.TotalHours;
            if (nowGameHours < _nextStrikeTime) return;

            if (_animationPlaying) return;

            float intervalGameHours = ComputeIntervalGameHours(power);
            _nextStrikeTime = nowGameHours + intervalGameHours;

            StartStrikeSequence(bellPos);
        }

        private float GetMechanicalPowerPercent()
        {
            try
            {
                Type mpType = Type.GetType("Vintagestory.GameContent.Mechanics.BEBehaviorMPConsumer, VSSurvivalMod");
                if (mpType == null) return 0f;
                var getBehaviorMethod = typeof(Vintagestory.API.Common.BlockEntity).GetMethod("GetBehavior");
                if (getBehaviorMethod == null) return 0f;
                var genericMethod = getBehaviorMethod.MakeGenericMethod(mpType);
                var beh = genericMethod.Invoke(this, null);
                if (beh == null) return 0f;
                // BEBehaviorMPConsumer exposes TrueSpeed (Network?.Speed * GearedRatio), not Speed
                PropertyInfo trueSpeedProp = mpType.GetProperty("TrueSpeed", BindingFlags.Public | BindingFlags.Instance);
                if (trueSpeedProp == null) return 0f;
                float speed = Convert.ToSingle(trueSpeedProp.GetValue(beh));
                MethodInfo getResistanceMethod = mpType.GetMethod("GetResistance", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                float resistance = getResistanceMethod != null ? Convert.ToSingle(getResistanceMethod.Invoke(beh, null)) : 0.1f;
                if (resistance <= 0) return Math.Min(1f, Math.Max(0f, speed));
                float power = speed / resistance;
                return Math.Min(1f, Math.Max(0f, power));
            }
            catch
            {
                return 0f;
            }
        }

        private static float ComputeIntervalGameHours(float powerPercent)
        {
            if (powerPercent <= MinPowerThreshold) return IntervalGameHoursAtMinPower;
            float t = (powerPercent - MinPowerThreshold) / (1f - MinPowerThreshold);
            return IntervalGameHoursAtMaxPower + (1f - t) * (IntervalGameHoursAtMinPower - IntervalGameHoursAtMaxPower);
        }

        private BlockPos? GetAdjacentBellPosition()
        {
            string side = GetBlockSide();
            BlockFacing face = BlockFacing.FromCode(side);
            if (face == null) return null;
            BlockPos front = Pos.AddCopy(face);
            if (IsBellBlock(Api.World.BlockAccessor.GetBlock(front)))
                return front;
            return null;
        }

        private bool IsBellBlock(Vintagestory.API.Common.Block block)
        {
            if (block == null) return false;
            string path = block.Code?.Path ?? "";
            return path.StartsWith("carillonbell") || path.StartsWith("churchbell");
        }

        private string GetBlockSide()
        {
            if (Block?.Variant == null) return "north";
            return Block.Variant.TryGetValue("side", out var side) ? side : "north";
        }

        private void StartStrikeSequence(BlockPos bellPos)
        {
            _animationPlaying = true;
            Animatable?.PlaySingleShotAnimation("strike");
            MarkDirty();

            if (_animationEndCallbackId >= 0)
                Api.World.UnregisterCallback(_animationEndCallbackId);

            _animationEndCallbackId = Api.World.RegisterCallback(_ =>
            {
                _animationEndCallbackId = -1;
                _animationPlaying = false;
                TriggerBell(bellPos);
                MarkDirty();
            }, (int)(AnimationToBellDelaySeconds * 1000));
        }

        private void TriggerBell(BlockPos bellPos)
        {
            var be = Api.World.BlockAccessor.GetBlockEntity(bellPos);
            if (be == null) return;

            if (be is BlockEntityCarillonBell carillon)
            {
                carillon.OnRung();
                return;
            }

            var soundable = be.GetBehavior<RPVoiceChat.GameContent.BlockEntityBehavior.BEBehaviorSoundable>();
            soundable?.OnRung();
        }

        public bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api?.Side != EnumAppSide.Server) return true;
            _enabled = !_enabled;
            MarkDirty();
            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            string enabledStr = _enabled ? UIUtils.I18n("BellHammer.Enabled") : UIUtils.I18n("BellHammer.Disabled");
            dsc.AppendLine(enabledStr);
            dsc.AppendLine(UIUtils.I18n("BellHammer.Power", (int)(PowerPercent * 100)));

            if (GetAdjacentBellPosition() == null)
                dsc.AppendLine(UIUtils.I18n("BellHammer.NoBell"));
        }
    }
}
