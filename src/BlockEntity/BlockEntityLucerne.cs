using System;
using System.IO;
using System.Text;
using RPVoiceChat.GameContent.Inventory;
using RPVoiceChat.GameContent.Renderers;
using RPVoiceChat.Gui;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityLucerne : BlockEntityOpenableContainer, RPVoiceChat.GameContent.IBlockEntityWithCustomLightPosition, IHeatSource
    {
        private InventoryLucerne _inventory;
        private bool _showStructureGuide;
        private bool _structureComplete;
        private double _burnEndTotalHours;
        private bool _showAshes; // true when beacon just went out, show ashes until 32 firewood is added again and lit
        private WarningBeaconGuideRenderer _guideRenderer;
        private WarningBeaconBonfireRenderer _bonfireRenderer;
        private long _flameParticlesTickListenerId;

        /// <summary>Firepit-style particles for the 3×3 zone when the beacon is lit. Matches vanilla firepit.json particlePropertiesByType["firepit-lit"] (fire quads, smoke quads, ember).</summary>
        private static SimpleParticleProperties _fireParticles;
        private static SimpleParticleProperties _smokeParticles;
        private static SimpleParticleProperties _emberParticles;

        public const double BurnDurationGameHours = 24.0;
        public const int FatRequired = 2;
        public const int FirewoodRequired = 32;

        static BlockEntityLucerne()
        {
            // Fire quads — orange/red; particle system uses BGRA so swap R/B vs ColorFromRgba(R,G,B,A)
            _fireParticles = new SimpleParticleProperties(
                2, 3,
                ColorUtil.ColorFromRgba(30, 120, 255, 255),
                new Vec3d(), new Vec3d(3, 1.5, 3),
                new Vec3f(0f, 0.15f, 0f), new Vec3f(0.12f, 0.06f, 0.12f))
            {
                LifeLength = 0.35f,
                GravityEffect = 0f,
                MinSize = 0.4f,
                MaxSize = 0.55f,
                ParticleModel = EnumParticleModel.Quad,
                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 0.35f),
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -14f),
                VertexFlags = 128,
                WindAffected = true
            };

            // Smoke quads — gray (BGRA swap for particle system)
            _smokeParticles = new SimpleParticleProperties(
                0, 1,
                ColorUtil.ColorFromRgba(55, 70, 85, 200),
                new Vec3d(), new Vec3d(3, 1.5, 3),
                new Vec3f(0f, 0.22f, 0f), new Vec3f(0.03f, 0.06f, 0.03f))
            {
                LifeLength = 20f,
                GravityEffect = 0f,
                MinSize = 0.35f,
                MaxSize = 0.5f,
                ParticleModel = EnumParticleModel.Quad,
                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 1.8f),
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -255f),
                SelfPropelled = true,
                WindAffected = true
            };

            // Ember (cubes) — orange/red (BGRA swap for particle system)
            _emberParticles = new SimpleParticleProperties(
                0, 1,
                ColorUtil.ColorFromRgba(25, 100, 255, 255),
                new Vec3d(), new Vec3d(3, 1.5, 3),
                new Vec3f(0f, 0.55f, 0f), new Vec3f(0.18f, 0.12f, 0.18f))
            {
                LifeLength = 1.8f,
                GravityEffect = -0.08f,
                MinSize = 0.35f,
                MaxSize = 0.5f,
                ParticleModel = EnumParticleModel.Cube,
                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -0.65f),
                VertexFlags = 128,
                WindAffected = true
            };
        }

        /// <summary>Computed from game's EnumBlockContainerPacketId so we never collide (see vssurvivalmod BEOpenableContainer.cs).</summary>
        public static readonly int PacketIdLightBeacon = GetCustomPacketIdBase() + 0;
        public static readonly int PacketIdSyncState = GetCustomPacketIdBase() + 1;
        /// <summary>Full inventory sync (TreeAttribute); avoids relying on game's InvNetworkUtil slot format with custom dialog.</summary>
        public static readonly int PacketIdInventory = GetCustomPacketIdBase() + 2;

        private static int GetCustomPacketIdBase()
        {
            try
            {
                var enumType = typeof(EnumBlockContainerPacketId);
                if (enumType == null || !enumType.IsEnum) return 10000;
                var values = Enum.GetValues(enumType);
                int max = 0;
                foreach (var v in values) { int i = (int)v; if (i > max) max = i; }
                return max + 1;
            }
            catch { return 10000; }
        }

        public override InventoryBase Inventory => _inventory;
        public override string InventoryClassName => "lucerne";

        public bool ShowStructureGuide => _showStructureGuide;
        public bool StructureComplete => _structureComplete;
        /// <summary>True when the block variant is "fired" (cooked); only then can the beacon hold fire, light, container and heat.</summary>
        public bool IsFired => (Block?.Variant["materialtype"] as string) == "fired";
        public bool IsBurning => Api?.World?.Calendar != null && Api.World.Calendar.TotalHours < _burnEndTotalHours;
        public double BurnTimeRemainingGameHours => IsBurning ? Math.Max(0, _burnEndTotalHours - Api.World.Calendar.TotalHours) : 0;

        public BlockEntityLucerne() { }

        public override void Initialize(ICoreAPI api)
        {
            if (_inventory == null)
                _inventory = new InventoryLucerne(api, Pos);
            base.Initialize(api);
            LateInitInventory();
            if (api.Side == EnumAppSide.Server)
            {
                (api as ICoreServerAPI)?.Event.RegisterGameTickListener(OnServerGameTick, 100);
                // Only fired lucernes reserve the 3×3 zone and validate the beacon structure.
                if (IsFired)
                    WarningBeaconStructure.RegisterLucerne(Pos, GetFacing());
            }
            if (api.Side == EnumAppSide.Client)
            {
                var capi = api as ICoreClientAPI;
                if (capi != null)
                {
                    capi.Event.EnqueueMainThreadTask(() =>
                    {
                        UpdateGuideRenderer();
                        UpdateBonfireRenderer();
                        _flameParticlesTickListenerId = capi.Event.RegisterGameTickListener(OnClientGameTickFlames, 4);
                    }, "rpvoicechat:InitBeaconRenderers");
                }
                RevalidateStructure();
            }
            else
                RevalidateStructure();
        }

        protected void LateInitInventory()
        {
            _inventory.LateInitialize(InventoryClassName + "-" + Pos, Api);
            _inventory.ResolveBlocksOrItems();
            _inventory.Pos ??= Pos;
            MarkDirty();
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (Api?.Side == EnumAppSide.Server && IsFired)
                RevalidateStructure();
        }

        public void ToggleStructureGuide(IPlayer byPlayer)
        {
            _showStructureGuide = !_showStructureGuide;
            MarkDirty(true);
        }

        private BlockFacing GetFacing()
        {
            var block = Api?.World?.BlockAccessor?.GetBlock(Pos);
            if (block == null) return BlockFacing.NORTH;
            string side = block.Variant["side"] as string ?? "north";
            return BlockFacing.FromCode(side);
        }

        public BlockPos GetStructureCenterWorldPos()
        {
            var c = WarningBeaconStructure.CenterLocal;
            return WarningBeaconStructure.LocalToWorld(Pos, c.X, c.Y, c.Z, GetFacing());
        }

        /// <summary>Geometric center of the reserved 3×3 zone (above platform), used for light position, particles and fire damage.</summary>
        public Vec3d GetStructureCenter3x3WorldPos() => GetStructureCenterWorldPos().ToWorldCenter();

        Vec3d IBlockEntityWithCustomLightPosition.GetLightOrigin() => GetStructureCenter3x3WorldPos();

        private void RevalidateStructure()
        {
            if (!IsFired) return;
            var accessor = Api?.World?.BlockAccessor;
            var block = accessor?.GetBlock(Pos);
            if (accessor == null || block == null) return;

            bool wasComplete = _structureComplete;
            WarningBeaconStructure.ValidateStructure(accessor, Pos, GetFacing(), block, out int filled, out int total);
            _structureComplete = (filled == total);
            if (!wasComplete && _structureComplete)
            {
                _showStructureGuide = false;
                MarkDirty(true);
                Api.World.PlaySoundAt(new AssetLocation("game", "sounds/block/rock"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, false, 8f, 0.5f);
            }
            else if (wasComplete && !_structureComplete)
                MarkDirty(true);
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!IsFired || !_structureComplete) return false;

            if (Api.World is IServerWorldAccessor)
            {
                byte[] data;
                using (var ms = new MemoryStream())
                {
                    var writer = new BinaryWriter(ms);
                    writer.Write("BlockEntityLucerne");
                    writer.Write(Lang.Get(RPVoiceChatMod.modID + ":WarningBeacon.GuiTitle"));
                    TreeAttribute tree = new TreeAttribute();
                    _inventory.ToTreeAttributes(tree);
                    tree.ToBytes(writer);
                    data = ms.ToArray();
                }
                ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer, Pos, (int)EnumBlockContainerPacketId.OpenInventory, data);
                byPlayer.InventoryManager.OpenInventory(_inventory);
            }
            return true;
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            if (packetid == PacketIdLightBeacon)
            {
                if (TryLightBeacon())
                {
                    var tree = new TreeAttribute();
                    ToTreeAttributes(tree);
                    using (var ms = new MemoryStream())
                    {
                        tree.ToBytes(new BinaryWriter(ms));
                        byte[] syncData = ms.ToArray();
                        (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdSyncState, syncData);
                    }
                }
                return;
            }
            if (packetid == PacketIdInventory && data != null && data.Length > 0)
            {
                try
                {
                    var tree = new TreeAttribute();
                    using (var ms = new MemoryStream(data))
                        tree.FromBytes(new BinaryReader(ms));
                    _inventory.FromTreeAttributes(tree);
                    _inventory.ResolveBlocksOrItems();
                    MarkDirty();
                }
                catch { /* ignore malformed packet */ }
                return;
            }
            base.OnReceivedClientPacket(fromPlayer, packetid, data);
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            var capi = Api as ICoreClientAPI;
            if (capi == null) return;

            if (packetid == PacketIdSyncState)
            {
                var syncTree = new TreeAttribute();
                using (var ms = new MemoryStream(data))
                    syncTree.FromBytes(new BinaryReader(ms));
                FromTreeAttributes(syncTree, capi.World);
                return;
            }

            if (packetid != (int)EnumBlockContainerPacketId.OpenInventory) return;

            string dialogTitle;
            TreeAttribute tree = new TreeAttribute();
            using (var ms = new MemoryStream(data))
            {
                var reader = new BinaryReader(ms);
                reader.ReadString();
                dialogTitle = reader.ReadString();
                tree.FromBytes(reader);
            }
            if (_inventory == null) { _inventory = new InventoryLucerne(Api, Pos); LateInitInventory(); }
            _inventory.FromTreeAttributes(tree);
            _inventory.ResolveBlocksOrItems();

            var dlg = new WarningBeaconDialog(dialogTitle, _inventory, Pos, capi, this, () => MarkDirty(true));
            dlg.TryOpen();
        }

        public bool TryLightBeacon()
        {
            if (!IsFired || Api?.Side != EnumAppSide.Server || !_structureComplete) return false;
            if (_inventory.FatSlot.StackSize < FatRequired || _inventory.FirewoodSlot.StackSize < FirewoodRequired)
                return false;

            _inventory.FatSlot.TakeOut(FatRequired);
            _inventory.FirewoodSlot.TakeOut(FirewoodRequired);
            MarkDirty();

            _burnEndTotalHours = Api.World.Calendar.TotalHours + BurnDurationGameHours;
            MarkDirty(true);

            var lightBhv = GetBehavior<BEBehaviorLightable>();
            if (lightBhv != null)
                lightBhv.SetLightActive(true);

            _showAshes = false;
            return true;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (_inventory != null)
            {
                var invTree = new TreeAttribute();
                _inventory.ToTreeAttributes(invTree);
                tree["inventory"] = invTree;
            }
            tree.SetBool("showGuide", _showStructureGuide);
            tree.SetBool("structureComplete", _structureComplete);
            tree.SetDouble("burnEndTotalHours", _burnEndTotalHours);
            tree.SetBool("showAshes", _showAshes);
            var lightBhv = GetBehavior<BEBehaviorLightable>();
            tree.SetBool("beaconLightActive", lightBhv?.IsLightActive ?? false);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            if (Api == null) Api = worldForResolving.Api;
            if (_inventory == null)
                _inventory = new InventoryLucerne(worldForResolving.Api, Pos);
            base.FromTreeAttributes(tree, worldForResolving);
            if (tree["inventory"] != null)
            {
                _inventory.FromTreeAttributes(tree["inventory"] as TreeAttribute);
                _inventory.ResolveBlocksOrItems();
            }
            if (_inventory != null)
                LateInitInventory();

            _showStructureGuide = tree.GetBool("showGuide", false);
            _structureComplete = tree.GetBool("structureComplete", false);
            _burnEndTotalHours = tree.GetDouble("burnEndTotalHours", 0);
            _showAshes = tree.GetBool("showAshes", false);
            bool beaconLightActive = tree.GetBool("beaconLightActive", false);

            if (Api?.Side == EnumAppSide.Client)
            {
                var lightBhv = GetBehavior<BEBehaviorLightable>();
                if (lightBhv != null)
                    lightBhv.SetLightActive(beaconLightActive);
                var capi = Api as ICoreClientAPI;
                if (capi != null)
                    capi.Event.EnqueueMainThreadTask(() => { UpdateGuideRenderer(); UpdateBonfireRenderer(); }, "rpvoicechat:UpdateBeaconRenderers");
            }
        }

        public bool ShowAshes => _showAshes;
        public bool HasEnoughFirewoodForBonfire => (_inventory?.FirewoodSlot?.StackSize ?? 0) >= FirewoodRequired;

        private void UpdateGuideRenderer()
        {
            if (Api?.Side != EnumAppSide.Client) return;
            var capi = Api as ICoreClientAPI;
            if (capi == null) return;
            if (_showStructureGuide && _guideRenderer == null)
                _guideRenderer = new WarningBeaconGuideRenderer(this, capi);
            else if (!_showStructureGuide && _guideRenderer != null)
            {
                _guideRenderer.Dispose();
                _guideRenderer = null;
            }
        }

        private void UpdateBonfireRenderer()
        {
            if (Api?.Side != EnumAppSide.Client) return;
            var capi = Api as ICoreClientAPI;
            if (capi == null) return;
            bool shouldShow = IsFired && _structureComplete && (_showAshes || HasEnoughFirewoodForBonfire || IsBurning);
            if (shouldShow && _bonfireRenderer == null)
                _bonfireRenderer = new WarningBeaconBonfireRenderer(this, capi);
            else if (!shouldShow && _bonfireRenderer != null)
            {
                _bonfireRenderer.Dispose();
                _bonfireRenderer = null;
            }
            else if (_bonfireRenderer != null)
                _bonfireRenderer.UpdateState();
        }

        /// <summary>Called by the client when the beacon GUI is closed so the bonfire/ashes renderer can update.</summary>
        public void RefreshBonfireRenderer()
        {
            if (Api?.Side == EnumAppSide.Client)
                UpdateBonfireRenderer();
        }

        public override void OnBlockBroken(IPlayer byPlayer)
        {
            if (Api?.Side == EnumAppSide.Server && IsFired)
                WarningBeaconStructure.UnregisterLucerne(Pos);
            DisposeClientRenderers();
            base.OnBlockBroken(byPlayer);
        }

        public override void OnBlockUnloaded()
        {
            if (Api?.Side == EnumAppSide.Server && IsFired)
                WarningBeaconStructure.UnregisterLucerne(Pos);
            DisposeClientRenderers();
            base.OnBlockUnloaded();
        }

        private void DisposeClientRenderers()
        {
            if (Api?.Side == EnumAppSide.Client && _flameParticlesTickListenerId != 0)
            {
                (Api as ICoreClientAPI)?.Event.UnregisterGameTickListener(_flameParticlesTickListenerId);
                _flameParticlesTickListenerId = 0;
            }
            _guideRenderer?.Dispose();
            _guideRenderer = null;
            _bonfireRenderer?.Dispose();
            _bonfireRenderer = null;
        }

        /// <summary>Same origin as WarningBeaconBonfireRenderer, offset 0.5 north and 0.5 west; corner of center block at platform top, so particles align with the bonfire and fill the 3×3.</summary>
        private void GetFlameSpawnBounds(out Vec3d minPos, out Vec3d addPos)
        {
            BlockPos center = GetStructureCenterWorldPos();
            // World: north = -Z, west = -X
            minPos = new Vec3d(center.X - 0.5 - 0.5, center.Y - 0.5, center.Z - 0.5 - 0.5);
            addPos = new Vec3d(3, 1.5, 3);
        }

        private void OnClientGameTickFlames(float dt)
        {
            if (!IsFired || !IsBurning || !_structureComplete || Api?.World == null) return;
            GetFlameSpawnBounds(out Vec3d minPos, out Vec3d addPos);
            var rnd = Api.World.Rand;

            _fireParticles.MinPos = minPos;
            _fireParticles.AddPos = addPos;
            Api.World.SpawnParticles(_fireParticles);

            if (rnd.NextDouble() < 0.14)
            {
                _smokeParticles.MinPos = minPos;
                _smokeParticles.AddPos = addPos;
                _smokeParticles.MinQuantity = 1;
                _smokeParticles.AddQuantity = 1;
                Api.World.SpawnParticles(_smokeParticles);
                _smokeParticles.MinQuantity = 0;
                _smokeParticles.AddQuantity = 1;
            }

            if (rnd.NextDouble() < 0.04)
            {
                _emberParticles.MinPos = minPos;
                _emberParticles.AddPos = addPos;
                _emberParticles.MinQuantity = 1;
                _emberParticles.AddQuantity = 1;
                Api.World.SpawnParticles(_emberParticles);
                _emberParticles.MinQuantity = 0;
                _emberParticles.AddQuantity = 1;
            }
        }

        private void OnServerGameTick(float dt)
        {
            RevalidateStructure();
            if (!IsBurning)
            {
                var lightBhv = GetBehavior<BEBehaviorLightable>();
                if (lightBhv != null && lightBhv.IsLightActive)
                {
                    lightBhv.SetLightActive(false);
                    _showAshes = true;
                    MarkDirty(true);
                }
                // When 32 firewood is added again after ashes, show bonfire shape (even before lighting)
                if (_showAshes && (_inventory?.FirewoodSlot?.StackSize ?? 0) >= FirewoodRequired)
                {
                    _showAshes = false;
                    MarkDirty(true);
                }
                return;
            }

            // Fire damage when entities are in the 3×3 zone (same idea as BlockFirepit.OnEntityInside; we tick every 100 ms so 0.05/tick ≈ 0.5/s)
            if (!IsFired || !_structureComplete || Block == null) return;
            var center = GetStructureCenter3x3WorldPos();
            var entities = Api.World.GetEntitiesAround(center, 1.5f, 1.5f);
            foreach (var entity in entities)
            {
                if (entity == null || !entity.Alive || Api.World.Rand.NextDouble() >= 0.05) continue;
                entity.ReceiveDamage(new DamageSource
                {
                    Source = EnumDamageSource.Block,
                    SourceBlock = Block,
                    Type = EnumDamageType.Fire,
                    SourcePos = center
                }, 0.5f);
            }
        }

        /// <summary>IHeatSource: heat only when the block is fired and the beacon is burning.</summary>
        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos) =>
            IsFired && IsBurning ? 3f : 0f;

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (!IsFired)
                return;
            if (_structureComplete)
            {
                if (!IsBurning)
                    dsc.AppendLine(Lang.Get(RPVoiceChatMod.modID + ":WarningBeacon.Ready"));
            }
            else
            {
                dsc.AppendLine(Lang.Get(RPVoiceChatMod.modID + ":WarningBeacon.BuildStructure"));
                dsc.AppendLine(Lang.Get(RPVoiceChatMod.modID + ":WarningBeacon.Interaction.ShowGuide"));
            }
        }
    }
}
