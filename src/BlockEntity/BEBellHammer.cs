#nullable enable

using System;
using System.Text;
using RPVoiceChat;
using RPVoiceChat.GameContent.BlockEntityBehavior;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent.Mechanics;
using RPVoiceChat.Util;

namespace RPVoiceChat.GameContent.BlockEntity
{
    /// <summary>
    /// Bell hammer: mechanical power consumer, plays animation then triggers adjacent carillon/church bell.
    /// Quern-like: strike rate scales with TrueSpeed (rotational speed). Min 25% speed required.
    /// At 25% speed: ~1 strike per in-game hour; at 100%: ~60 strikes per in-game hour (≈ 1s IRL). Right-click to enable/disable.
    /// </summary>
    public class BlockEntityBellHammer : Vintagestory.API.Common.BlockEntity
    {
        /// <summary>Minimum rotational speed (TrueSpeed) required to operate, same as Quern logic.</summary>
        public const float MinSpeedThreshold = 0.25f;
        /// <summary>Strikes per in-game hour at minimum speed (25%).</summary>
        public const float StrikeRateAtMinSpeed = 1f;
        /// <summary>Strikes per in-game hour at max speed (100%).</summary>
        public const float StrikeRateAtMaxSpeed = 60f;
        public const float AnimationToBellDelaySeconds = 0.4f;

        private bool _enabled;
        private float _strikeProgress;
        private double _lastTotalHours;
        private bool _animationPlaying;
        private bool _hadBellLastTick;
        private long _animationEndCallbackId = -1;
        private int _lastSyncedPowerPercent = -1;
        private bool _lastGearActive;

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
            {
                string shapePath = Block?.Shape?.Base?.Path ?? "block/bellhammer/bellhammer";
                bool isCeiling = Block?.Variant?.TryGetValue("v", out var v) == true && string.Equals(v, "down", StringComparison.OrdinalIgnoreCase);
                if (isCeiling && (shapePath == null || shapePath.IndexOf("bellhammer_chains", StringComparison.OrdinalIgnoreCase) < 0))
                    shapePath = "block/bellhammer/bellhammer_chains";
                float rotDeg = Block?.Variant?.TryGetValue("side", out var side) == true
                    ? side switch { "north" => 90f, "east" => 0f, "south" => 270f, "west" => 180f, _ => 0f }
                    : 0f;
                Animatable?.InitializeAnimatorWithRotation(shapePath, rotDeg);
            }
            if (api.Side == EnumAppSide.Server)
            {
                (api as ICoreServerAPI)?.Event.RegisterGameTickListener(OnServerGameTick, 100);
                TryDiscoverNetwork();
            }
        }

        /// <summary>
        /// Forces the MPConsumer to discover the network in the connector direction (axle side).
        /// Always horizontal: connector is the face opposite to "side" (bell in front, axle on the other side).
        /// </summary>
        public void TryDiscoverNetwork()
        {
            if (Block?.Variant == null || !Block.Variant.TryGetValue("side", out string sideStr)) return;
            BlockFacing frontFace = BlockFacing.FromCode(sideStr);
            if (frontFace == null) return;
            BlockFacing connectorFace = frontFace.Opposite;

            var mechBase = GetBehavior<BEBehaviorMPBase>();
            if (mechBase == null) return;

            mechBase.CreateJoinAndDiscoverNetwork(connectorFace);
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
            _enabled = tree.GetBool("enabled", false);
            PowerPercent = tree.GetFloat("powerPercent", 0f);
            bool gearActive = _enabled && PowerPercent >= MinSpeedThreshold;
            if (gearActive != _lastGearActive)
            {
                _lastGearActive = gearActive;
                if (gearActive)
                    Animatable?.StartAnimationIfNotRunning("gear");
                else
                    Animatable?.StopAnimation("gear");
            }
        }

        private void OnServerGameTick(float dt)
        {
            if (!_enabled || Block == null) return;
            if (Api?.World?.BlockAccessor?.GetBlockEntity(Pos) != this) return;

            // Quern-like: use TrueSpeed (rotational speed) directly – more speed = faster strikes
            float speed = GetTrueSpeed();
            PowerPercent = speed;

            int percentDisplay = (int)(speed * 100);
            if (percentDisplay != _lastSyncedPowerPercent)
            {
                _lastSyncedPowerPercent = percentDisplay;
                MarkDirty();
            }

            if (speed < MinSpeedThreshold) return;

            BlockPos? bellPos = GetAdjacentBellPosition();
            if (bellPos == null)
            {
                _hadBellLastTick = false;
                return;
            }
            if (!_hadBellLastTick)
            {
                _hadBellLastTick = true;
                _lastTotalHours = Api.World.Calendar.TotalHours;
                _strikeProgress = 0f;
            }

            if (_animationPlaying) return;

            // Accumulate progress each tick based on current speed (handles speed changes in real time)
            double nowTotalHours = Api.World.Calendar.TotalHours;
            float gameHoursThisTick = (float)(nowTotalHours - _lastTotalHours);
            _lastTotalHours = nowTotalHours;
            if (gameHoursThisTick > 0 && gameHoursThisTick < 1f) // sanity: ignore huge jumps (e.g. load)
                _strikeProgress += gameHoursThisTick * ComputeStrikeRatePerGameHour(speed);
            if (_strikeProgress < 1f) return;

            _strikeProgress -= 1f;
            StartStrikeSequence(bellPos);
        }

        /// <summary>
        /// Rotational speed from the mechanical network (TrueSpeed), 0–1. Same as Quern: actual RPM drives effect rate.
        /// </summary>
        private float GetTrueSpeed()
        {
            var consumer = GetBehavior<BEBehaviorMPConsumer>();
            if (consumer == null) return 0f;
            return GameMath.Clamp(consumer.TrueSpeed, 0f, 1f);
        }

        /// <summary>
        /// Strike rate (strikes per game hour) scales linearly with speed.
        /// At 25% speed: 1/h. At 100%: 60/h. Progress accumulated each tick uses current speed.
        /// </summary>
        private static float ComputeStrikeRatePerGameHour(float speed)
        {
            if (speed <= MinSpeedThreshold) return StrikeRateAtMinSpeed;
            float t = (speed - MinSpeedThreshold) / (1f - MinSpeedThreshold);
            return StrikeRateAtMinSpeed + t * (StrikeRateAtMaxSpeed - StrikeRateAtMinSpeed);
        }

        private BlockPos? GetAdjacentBellPosition()
        {
            BlockFacing face = GetBellDirection();
            if (face == null) return null;
            var ba = Api.World.BlockAccessor;
            BlockPos front1 = Pos.AddCopy(face);
            var block1 = ba.GetBlock(front1);
            if (IsBellBlock(block1))
                return front1;
            BlockPos front2 = front1.AddCopy(face);
            // Only churchbell (larger) is detected at 2 blocks; carillonbell is 1 block only.
            if (IsChurchBellBlock(ba.GetBlock(front2)))
                return front2;
            return null;
        }

        /// <summary>Direction toward the bell: always horizontal (same level), via the "side" variant.</summary>
        private BlockFacing GetBellDirection()
        {
            string side = GetBlockSide();
            return BlockFacing.FromCode(side);
        }

        private static bool IsBellBlock(Vintagestory.API.Common.Block block)
        {
            if (block == null) return false;
            string path = block.Code?.Path ?? "";
            return path.StartsWith("carillonbell") || path.StartsWith("churchbell");
        }

        private static bool IsChurchBellBlock(Vintagestory.API.Common.Block block)
        {
            if (block == null) return false;
            string path = block.Code?.Path ?? "";
            return path.StartsWith("churchbell");
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
                if (Api?.World?.BlockAccessor?.GetBlockEntity(Pos) == this)
                {
                    TriggerBell(bellPos);
                    if (_enabled && PowerPercent >= MinSpeedThreshold)
                        Animatable?.StartAnimationIfNotRunning("gear");
                    MarkDirty();
                }
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
            if (_enabled)
            {
                TryDiscoverNetwork();
                _lastTotalHours = Api.World.Calendar.TotalHours;
                _strikeProgress = 0f;
            }
            else
            {
                _lastGearActive = false;
                Animatable?.StopAnimation("gear");
            }
            MarkDirty();
            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            string enabledStr = _enabled ? UIUtils.I18n("BellHammer.Enabled") : UIUtils.I18n("BellHammer.Disabled");
            dsc.AppendLine(enabledStr);
            dsc.AppendLine(UIUtils.I18n("BellHammer.Power", (int)(PowerPercent * 100)));

            if (GetAdjacentBellPosition() == null)
                dsc.AppendLine(UIUtils.I18n("BellHammer.NoBell"));
        }
    }
}
