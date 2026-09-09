using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioReceiver : BEWireNode, IWireTypedNode
    {
        public const int MinPlaybackRangeBlocks = 0;
        public const int MaxPlaybackRangeBlocks = 15;
        public const int DefaultPlaybackRangeBlocks = 8;
        public const int MaxWiredSpeakers = 4;
        public const int MinPlaybackVolumePercent = 0;
        public const int MaxPlaybackVolumePercent = 100;
        public const int DefaultPlaybackVolumePercent = 100;

        private RadioReceiverDialog dialog;
        private string tunedFrequency = "100.0";
        private bool isEnabled = false;
        private int playbackRangeBlocks = DefaultPlaybackRangeBlocks;
        private int playbackVolumePercent = DefaultPlaybackVolumePercent;
        private string heardStationName = "";
        private long stationNameListenerId = -1;

        public string TunedFrequency => tunedFrequency ?? "";
        public bool IsEnabled => isEnabled;
        public int PlaybackRangeBlocks => playbackRangeBlocks;
        public int PlaybackVolumePercent => playbackVolumePercent;
        public string HeardStationName => heardStationName ?? "";

        protected override int MaxConnections => 1;
        public override bool IsActiveEndpoint => true;
        public WireNodeKind WireNodeKind => WireNodeKind.RadioReceiver;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            playbackRangeBlocks = GameMath.Clamp(playbackRangeBlocks, MinPlaybackRangeBlocks, MaxPlaybackRangeBlocks);
            playbackVolumePercent = GameMath.Clamp(playbackVolumePercent, MinPlaybackVolumePercent, MaxPlaybackVolumePercent);

            if (api.Side == EnumAppSide.Server)
            {
                stationNameListenerId = api.Event.RegisterGameTickListener(OnServerStationNameTick, 1000);
                PublishRfPresence();
            }
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                return true;
            }

            if (Api is not ICoreClientAPI capi)
            {
                return true;
            }

            if (dialog?.IsOpened() == true)
            {
                return true;
            }

            dialog = new RadioReceiverDialog(capi, this);
            dialog.TryOpen();
            return true;
        }

        public void RequestSetFrequency(string desired)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetReceiverFrequency,
                Value = desired ?? ""
            });
        }

        public void RequestSetEnabled(bool enabled)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetReceiverEnabled,
                IntValue = enabled ? 1 : 0
            });
        }

        public void RequestSetPlaybackRange(int rangeBlocks)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetReceiverPlaybackRange,
                IntValue = GameMath.Clamp(rangeBlocks, MinPlaybackRangeBlocks, MaxPlaybackRangeBlocks)
            });
        }

        public void RequestSetPlaybackVolume(int volumePercent)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetReceiverPlaybackVolume,
                IntValue = GameMath.Clamp(volumePercent, MinPlaybackVolumePercent, MaxPlaybackVolumePercent)
            });
        }

        public static float GetPlaybackVolumeGainAtSource(ICoreClientAPI capi, Vec3d sourceOverride, int dimension)
        {
            if (capi?.World?.BlockAccessor == null || sourceOverride == null)
            {
                return -1f;
            }

            BlockPos blockPos = new BlockPos(
                (int)System.Math.Floor(sourceOverride.X),
                (int)System.Math.Floor(sourceOverride.Y),
                (int)System.Math.Floor(sourceOverride.Z));
            blockPos.dimension = dimension;

            Vintagestory.API.Common.BlockEntity blockEntity = capi.World.BlockAccessor.GetBlockEntity(blockPos);
            if (blockEntity is BlockEntityRadioReceiver receiver)
            {
                return receiver.PlaybackVolumePercent / 100f;
            }

            if (blockEntity is BlockEntitySpeaker speaker)
            {
                foreach (BlockEntityRadioReceiver wiredReceiver in RadioWireNetworkHelper.FindReceivers(speaker))
                {
                    return wiredReceiver.PlaybackVolumePercent / 100f;
                }
            }

            return -1f;
        }

        public void SetTunedFrequency(string desired)
        {
            tunedFrequency = (desired ?? "").Trim();
            RefreshHeardStationName(force: true);
            MarkDirty();
            dialog?.RefreshData();
            PublishRfPresence();
        }

        public void SetEnabled(bool enabled)
        {
            if (isEnabled == enabled)
            {
                return;
            }

            isEnabled = enabled;
            MarkDirty(true);
            dialog?.RefreshData();
            PublishRfPresence();
        }

        public void SetPlaybackRange(int rangeBlocks)
        {
            int clamped = GameMath.Clamp(rangeBlocks, MinPlaybackRangeBlocks, MaxPlaybackRangeBlocks);
            if (playbackRangeBlocks == clamped)
            {
                return;
            }

            playbackRangeBlocks = clamped;
            MarkDirty(true);
            dialog?.RefreshData();
            PublishRfPresence();
        }

        public void SetPlaybackVolume(int volumePercent)
        {
            int clamped = GameMath.Clamp(volumePercent, MinPlaybackVolumePercent, MaxPlaybackVolumePercent);
            if (playbackVolumePercent == clamped)
            {
                return;
            }

            playbackVolumePercent = clamped;
            MarkDirty(true);
            dialog?.RefreshData();
        }

        /// <summary>
        /// Publishes receiver + wired speaker acoustic points so RF→local playback survives chunk unload.
        /// </summary>
        public void PublishRfPresence()
        {
            if (Api?.Side != EnumAppSide.Server || Pos == null)
            {
                return;
            }

            var acousticPoints = new System.Collections.Generic.List<RadioRfAcousticPresence>
            {
                new RadioRfAcousticPresence
                {
                    Pos = Pos.Copy(),
                    Dimension = Pos.dimension,
                    RangeBlocks = playbackRangeBlocks
                }
            };

            foreach (var speaker in RadioWireNetworkHelper.FindSpeakers(this))
            {
                if (speaker?.Pos == null)
                {
                    continue;
                }

                acousticPoints.Add(new RadioRfAcousticPresence
                {
                    Pos = speaker.Pos.Copy(),
                    Dimension = speaker.Pos.dimension,
                    RangeBlocks = speaker.VoiceEmissionRangeBlocks
                });
            }

            RadioRfPresenceRegistry.UpsertReceiver(new RadioRfReceiverPresence
            {
                Pos = Pos.Copy(),
                Dimension = Pos.dimension,
                TunedFrequency = RadioFrequencyUtil.Normalize(tunedFrequency),
                PlaybackRangeBlocks = playbackRangeBlocks,
                IsEnabled = isEnabled,
                AcousticPoints = acousticPoints
            });
        }

        private void OnServerStationNameTick(float dt)
        {
            RefreshHeardStationName(force: false);
        }

        private void RefreshHeardStationName(bool force)
        {
            if (Api?.Side != EnumAppSide.Server || Api.World == null)
            {
                return;
            }

            string resolved = "";
            if (isEnabled)
            {
                resolved = RadioStationNameResolver.Resolve(Api.World, tunedFrequency);
            }

            if (!force && string.Equals(heardStationName, resolved, System.StringComparison.Ordinal))
            {
                return;
            }

            heardStationName = resolved;
            MarkDirty();
            dialog?.RefreshData();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            tunedFrequency = tree.GetString("rpvc:radioReceiverFrequency", tunedFrequency);
            isEnabled = tree.GetBool("rpvc:radioReceiverEnabled", false);
            playbackRangeBlocks = GameMath.Clamp(
                tree.GetInt("rpvc:radioReceiverPlaybackRange", DefaultPlaybackRangeBlocks),
                MinPlaybackRangeBlocks,
                MaxPlaybackRangeBlocks);
            playbackVolumePercent = GameMath.Clamp(
                tree.GetInt("rpvc:radioReceiverPlaybackVolume", DefaultPlaybackVolumePercent),
                MinPlaybackVolumePercent,
                MaxPlaybackVolumePercent);
            heardStationName = tree.GetString("rpvc:radioReceiverHeardStationName", heardStationName) ?? "";
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("rpvc:radioReceiverFrequency", tunedFrequency ?? "");
            tree.SetBool("rpvc:radioReceiverEnabled", isEnabled);
            tree.SetInt("rpvc:radioReceiverPlaybackRange", playbackRangeBlocks);
            tree.SetInt("rpvc:radioReceiverPlaybackVolume", playbackVolumePercent);
            tree.SetString("rpvc:radioReceiverHeardStationName", heardStationName ?? "");
        }

        public override void OnBlockRemoved()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioRfPresenceRegistry.RemoveReceiver(Pos);
            }

            Unregister();
            base.OnBlockRemoved();
            dialog?.TryClose();
        }

        public override void OnBlockUnloaded()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                PublishRfPresence();
            }

            Unregister();
            base.OnBlockUnloaded();
        }

        private void Unregister()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                if (stationNameListenerId != -1)
                {
                    Api.Event.UnregisterGameTickListener(stationNameListenerId);
                    stationNameListenerId = -1;
                }
            }
        }
    }
}
