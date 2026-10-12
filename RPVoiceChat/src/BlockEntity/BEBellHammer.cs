#nullable enable

using System;
using System.Diagnostics;
using System.Text;
using RPVoiceChat;
using RPVoiceChat.GameContent.BlockEntityBehavior;
using RPVoiceChat.GameContent.Renderers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;
using RPVoiceChat.Util;

namespace RPVoiceChat.GameContent.BlockEntity
{
    /// <summary>
    /// Bell hammer: mechanical power consumer, plays animation then triggers adjacent carillon/church bell.
    /// Quern-like: strike rate scales with TrueSpeed (rotational speed). Min 25% speed required.
    /// At 25% speed: one strike per seven game days; at 100%: one per game day. Right-click to enable/disable.
    /// </summary>
    public class BlockEntityBellHammer : Vintagestory.API.Common.BlockEntity
    {
        private const string StrikeAnimationCode = "strike";
        private const string GearAnimationCode = "gear";
        /// <summary>Minimum rotational speed (TrueSpeed) required to operate, same as Quern logic.</summary>
        public const float MinSpeedThreshold = 0.25f;
        public const float AnimationToBellDelaySeconds = 0.4f;

        private bool _enabled;
        private bool _useRealTime;
        private long _lastRealTimestamp;
        private long _lastProgressSaveTimestamp;
        private long _lastUtcMilliseconds;
        private bool _restoreTimingPending;
        private double _strikeProgress;
        private double _lastTotalHours;
        private bool _animationPlaying;
        private bool _hadBellLastTick;
        private long _animationEndCallbackId = -1;
        private int _lastSyncedPowerPercent = -1;
        private int _strikeSequence;
        private int _clientLastStrikeSequence = -1;
        private bool _syncedGearActive;
        private RotatingMechPartRenderer? _mechPartRenderer;

        private Vintagestory.GameContent.BEBehaviorAnimatable? Animatable => GetBehavior<Vintagestory.GameContent.BEBehaviorAnimatable>();
        private BlockEntityAnimationUtil? AnimUtil => Animatable?.animUtil;

        public bool Enabled => _enabled;
        public float PowerPercent { get; private set; }

        public BlockEntityBellHammer()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            DisableConsumerInstancedRenderer();
            if (api.Side == EnumAppSide.Client)
            {
                string shapePath = Block?.Shape?.Base?.Path ?? "block/bellhammer/bellhammer";
                bool isCeiling = Block?.Variant?.TryGetValue("v", out var v) == true && string.Equals(v, "down", StringComparison.OrdinalIgnoreCase);
                if (isCeiling && (shapePath == null || shapePath.IndexOf("bellhammer_chains", StringComparison.OrdinalIgnoreCase) < 0))
                    shapePath = "block/bellhammer/bellhammer_chains";
                float rotDeg = Block?.Variant?.TryGetValue("side", out var side) == true
                    ? side switch { "north" => 90f, "east" => 0f, "south" => 270f, "west" => 180f, _ => 0f }
                    : 0f;
                InitializeClientAnimator(shapePath, rotDeg);

                if (api is ICoreClientAPI capi)
                {
                    _mechPartRenderer = new RotatingMechPartRenderer(
                        this,
                        capi,
                        new AssetLocation("rpvoicechat:shapes/block/bellhammer/bellhammer_mechpart.json"),
                        GetMechPartBaseRotY()
                    );
                }
            }
            if (api.Side == EnumAppSide.Server)
            {
                if (!_restoreTimingPending)
                {
                    _lastTotalHours = api.World.Calendar.TotalHours;
                    _lastUtcMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
                _lastRealTimestamp = _lastProgressSaveTimestamp = Stopwatch.GetTimestamp();
                (api as ICoreServerAPI)?.Event.RegisterGameTickListener(OnServerGameTick, 100);
                TryDiscoverNetwork();
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Block == null) return false;
            if (AnimUtil?.activeAnimationsByAnimCode?.Count > 0 ||
                (AnimUtil?.animator != null && AnimUtil.animator.ActiveAnimationCount > 0))
            {
                // Avoid a second static copy while Animatable renders the animated mesh.
                return true;
            }

            // Quern-style: tesselate static mesh explicitly, skip default aggregation.
            CompositeShape blockShape = Block.Shape;
            if (blockShape?.Base == null) return false;

            AssetLocation shapeLoc = blockShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = Shape.TryGet(Api, shapeLoc);
            if (shape == null) return false;

            tesselator.TesselateShape(
                Block,
                shape,
                out MeshData mesh,
                new Vec3f(blockShape.rotateX, blockShape.rotateY, blockShape.rotateZ),
                blockShape.QuantityElements,
                blockShape.SelectiveElements
            );
            mesher.AddMeshData(mesh);

            return true;
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
            _mechPartRenderer?.Dispose();
            _mechPartRenderer = null;
            if (Api?.Side == EnumAppSide.Server && _animationEndCallbackId >= 0)
                Api.World.UnregisterCallback(_animationEndCallbackId);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            _mechPartRenderer?.Dispose();
            _mechPartRenderer = null;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("enabled", _enabled);
            tree.SetBool("rpvc:bhRealTime", _useRealTime);
            tree.SetDouble("rpvc:bhProgress", _strikeProgress);
            tree.SetLong("rpvc:bhLastUtcMs", _lastUtcMilliseconds);
            tree.SetDouble("rpvc:bhLastGameHours", _lastTotalHours);
            tree.SetBool("rpvc:bhHadBell", _hadBellLastTick);
            tree.SetFloat("powerPercent", PowerPercent);
            tree.SetBool("rpvc:bhGearActive", _syncedGearActive);
            tree.SetInt("rpvc:bhStrikeSequence", _strikeSequence);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _enabled = tree.GetBool("enabled", false);
            _useRealTime = tree.GetBool("rpvc:bhRealTime", false);
            double savedProgress = tree.GetDouble("rpvc:bhProgress", 0);
            _strikeProgress = double.IsFinite(savedProgress) ? Math.Max(savedProgress, 0d) : 0;
            _lastUtcMilliseconds = tree.GetLong("rpvc:bhLastUtcMs", 0);
            _lastTotalHours = tree.GetDouble("rpvc:bhLastGameHours", 0);
            _hadBellLastTick = tree.GetBool("rpvc:bhHadBell", false);
            _restoreTimingPending = worldForResolving.Side == EnumAppSide.Server && _lastUtcMilliseconds > 0;
            PowerPercent = tree.GetFloat("powerPercent", 0f);
            _syncedGearActive = tree.GetBool("rpvc:bhGearActive", false);
            _strikeSequence = tree.GetInt("rpvc:bhStrikeSequence", 0);
            DisableConsumerInstancedRenderer();

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                string shapePath = Block?.Shape?.Base?.Path ?? "block/bellhammer/bellhammer";
                bool isCeiling = Block?.Variant?.TryGetValue("v", out var v) == true && string.Equals(v, "down", StringComparison.OrdinalIgnoreCase);
                if (isCeiling && (shapePath == null || shapePath.IndexOf("bellhammer_chains", StringComparison.OrdinalIgnoreCase) < 0))
                    shapePath = "block/bellhammer/bellhammer_chains";
                float rotDeg = Block?.Variant?.TryGetValue("side", out var side) == true
                    ? side switch { "north" => 90f, "east" => 0f, "south" => 270f, "west" => 180f, _ => 0f }
                    : 0f;
                InitializeClientAnimator(shapePath, rotDeg);

                if (_syncedGearActive)
                    StartAnimationIfNotRunning(GearAnimationCode);
                else
                    StopAnimation(GearAnimationCode);

                if (_clientLastStrikeSequence < 0)
                {
                    _clientLastStrikeSequence = _strikeSequence;
                }
                else if (_strikeSequence != _clientLastStrikeSequence)
                {
                    PlaySingleShotAnimation(StrikeAnimationCode);
                    _clientLastStrikeSequence = _strikeSequence;
                }
            }
        }

        private void OnServerGameTick(float dt)
        {
            if (Api?.World?.BlockAccessor?.GetBlockEntity(Pos) != this) return;
            var calendar = Api.World.Calendar;
            long realTimestamp = Stopwatch.GetTimestamp();
            double realHoursThisTick = Stopwatch.GetElapsedTime(_lastRealTimestamp, realTimestamp).TotalHours;
            _lastRealTimestamp = realTimestamp;
            double nowTotalHours = calendar.TotalHours;
            double gameHoursThisTick = nowTotalHours - _lastTotalHours;
            long utcNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_restoreTimingPending)
            {
                // Mechanical networks do not simulate unloaded chunks. Extrapolate
                // using the last saved enabled state, bell presence and speed.
                double unloadedHours = _useRealTime
                    ? (utcNow - (double)_lastUtcMilliseconds) / 3600000d
                    : gameHoursThisTick;
                if (_enabled && _hadBellLastTick && PowerPercent >= MinSpeedThreshold
                    && double.IsFinite(unloadedHours) && unloadedHours > 0)
                    _strikeProgress += unloadedHours * ComputeStrikeRatePerHour(PowerPercent,
                        _useRealTime ? 24d : calendar.HoursPerDay);
                // Coalesce missed strikes into one on return, keeping fractional progress.
                if (_strikeProgress >= 1) _strikeProgress = 1 + _strikeProgress % 1;
                _restoreTimingPending = false;
                realHoursThisTick = gameHoursThisTick = 0;
                MarkDirty();
            }
            _lastUtcMilliseconds = utcNow;
            // Always advance the baseline, including while disabled or underpowered.
            _lastTotalHours = nowTotalHours;
            if (!_enabled || Block == null) return;

            // Use the actual rotational speed to interpolate the calendar-based rates.
            float speed = GetTrueSpeed();
            PowerPercent = speed;

            int percentDisplay = (int)(speed * 100);
            bool gearActiveNow = _enabled && speed >= MinSpeedThreshold;
            if (gearActiveNow != _syncedGearActive)
            {
                _syncedGearActive = gearActiveNow;
                MarkDirty();
            }
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
                _strikeProgress = 0;
                return;
            }
            if (!_hadBellLastTick)
            {
                _hadBellLastTick = true;
                gameHoursThisTick = 0;
                realHoursThisTick = 0;
            }

            // A monotonic real clock ignores calendar acceleration and system clock edits.
            // Unloaded time is recovered separately from persisted clock readings.
            double elapsedHours = _useRealTime ? realHoursThisTick : gameHoursThisTick;
            double hoursPerDay = _useRealTime ? 24d : calendar.HoursPerDay;
            // Accumulate during the strike animation too, without overlapping strikes.
            if (double.IsFinite(elapsedHours) && elapsedHours > 0)
                _strikeProgress += elapsedHours * ComputeStrikeRatePerHour(speed, hoursPerDay);
            if (Stopwatch.GetElapsedTime(_lastProgressSaveTimestamp, realTimestamp).TotalSeconds >= 60)
            {
                _lastProgressSaveTimestamp = realTimestamp;
                MarkDirty();
            }
            if (_animationPlaying || _strikeProgress < 1f) return;

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
        /// Strike rate in the selected clock's hours scales linearly with speed.
        /// At 25%: one strike per seven days. At 100%: one per day.
        /// </summary>
        private static double ComputeStrikeRatePerHour(float speed, double hoursPerDay)
        {
            if (!double.IsFinite(hoursPerDay) || hoursPerDay <= 0) return 0;
            double weeklyRate = 1d / (7d * hoursPerDay);
            double dailyRate = 1d / hoursPerDay;
            double t = Math.Clamp((speed - MinSpeedThreshold) / (1d - MinSpeedThreshold), 0d, 1d);
            return weeklyRate + t * (dailyRate - weeklyRate);
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
            _strikeSequence++;
            _syncedGearActive = false;
            MarkDirty(true);

            if (_animationEndCallbackId >= 0)
                Api.World.UnregisterCallback(_animationEndCallbackId);

            _animationEndCallbackId = Api.World.RegisterCallback(_ =>
            {
                _animationEndCallbackId = -1;
                _animationPlaying = false;
                if (Api?.World?.BlockAccessor?.GetBlockEntity(Pos) == this)
                {
                    TriggerBell(bellPos);
                    _syncedGearActive = _enabled && PowerPercent >= MinSpeedThreshold;
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
            var heldCode = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.Code;
            if (heldCode?.Domain == "game" && heldCode.Path == "gear-temporal")
            {
                _useRealTime = !_useRealTime;
                _lastTotalHours = Api.World.Calendar.TotalHours;
                _lastRealTimestamp = Stopwatch.GetTimestamp();
                _lastUtcMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _restoreTimingPending = false;
                MarkDirty();
                return true;
            }
            _enabled = !_enabled;
            if (_enabled)
            {
                TryDiscoverNetwork();
                _lastTotalHours = Api.World.Calendar.TotalHours;
                _lastRealTimestamp = Stopwatch.GetTimestamp();
                _lastUtcMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            else
            {
                _syncedGearActive = false;
                StopAnimation(GearAnimationCode);
            }
            MarkDirty();
            return true;
        }

        private void InitializeClientAnimator(string shapePath, float rotDeg)
        {
            var animUtil = AnimUtil;
            if (animUtil == null || animUtil.animator != null || Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            if (Block?.Code != null && !string.IsNullOrWhiteSpace(shapePath))
            {
                var assetLoc = new AssetLocation(Block.Code.Domain, "shapes/" + shapePath + ".json");
                var shape = Shape.TryGet(Api, assetLoc);
                if (shape?.Animations != null && shape.Animations.Length > 0)
                {
                    shape.InitForAnimations(Api.Logger, shapePath, Array.Empty<string>());
                }
            }

            animUtil.InitializeAnimator(shapePath, null, null, new Vec3f(0, rotDeg, 0));
        }

        private void StartAnimationIfNotRunning(string animationCode)
        {
            var animUtil = AnimUtil;
            if (animUtil == null) return;
            if (animUtil.activeAnimationsByAnimCode.ContainsKey(animationCode)) return;

            animUtil.StartAnimation(new AnimationMetaData
            {
                Animation = animationCode,
                Code = animationCode
            });
        }

        private void StopAnimation(string animationCode)
        {
            AnimUtil?.StopAnimation(animationCode);
        }

        private void PlaySingleShotAnimation(string animationCode)
        {
            var animUtil = AnimUtil;
            if (animUtil == null) return;
            if (animUtil.activeAnimationsByAnimCode.ContainsKey(animationCode))
            {
                animUtil.StopAnimation(animationCode);
            }

            animUtil.StartAnimation(new AnimationMetaData
            {
                Animation = animationCode,
                Code = animationCode
            });
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            string enabledStr = _enabled ? UIUtils.I18n("BellHammer.Enabled") : UIUtils.I18n("BellHammer.Disabled");
            dsc.AppendLine(enabledStr);
            dsc.AppendLine(UIUtils.I18n(_useRealTime ? "BellHammer.RealTime" : "BellHammer.GameTime"));
            dsc.AppendLine(UIUtils.I18n("BellHammer.Power", (int)(PowerPercent * 100)));

            if (GetAdjacentBellPosition() == null)
                dsc.AppendLine(UIUtils.I18n("BellHammer.NoBell"));
        }

        private void DisableConsumerInstancedRenderer()
        {
            var consumer = GetBehavior<BEBehaviorMPConsumer>();
            if (consumer == null) return;
            consumer.Shape = null;
        }

        private float GetMechPartBaseRotY()
        {
            if (Block?.Variant?.TryGetValue("side", out var side) != true) return 0f;
            return side switch
            {
                "north" => 90f,
                "east" => 0f,
                "south" => 270f,
                "west" => 180f,
                _ => 0f
            };
        }

    }
}
