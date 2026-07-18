using RPVoiceChat.Gui;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRadioReceiver : Vintagestory.API.Common.BlockEntity
    {
        public const int MinPlaybackRangeBlocks = 0;
        public const int MaxPlaybackRangeBlocks = 15;
        public const int DefaultPlaybackRangeBlocks = 8;

        private RadioReceiverDialog dialog;
        private string tunedFrequency = "100.0";
        private bool isEnabled = false;
        private int playbackRangeBlocks = DefaultPlaybackRangeBlocks;
        private string heardStationName = "";
        private long stationNameListenerId = -1;

        public string TunedFrequency => tunedFrequency ?? "";
        public bool IsEnabled => isEnabled;
        public int PlaybackRangeBlocks => playbackRangeBlocks;
        public string HeardStationName => heardStationName ?? "";

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            playbackRangeBlocks = GameMath.Clamp(playbackRangeBlocks, MinPlaybackRangeBlocks, MaxPlaybackRangeBlocks);

            if (api.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.RegisterReceiver(Pos);
                stationNameListenerId = api.Event.RegisterGameTickListener(OnServerStationNameTick, 1000);
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

        public void SetTunedFrequency(string desired)
        {
            tunedFrequency = (desired ?? "").Trim();
            RefreshHeardStationName(force: true);
            MarkDirty();
            dialog?.RefreshData();
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
            heardStationName = tree.GetString("rpvc:radioReceiverHeardStationName", heardStationName) ?? "";
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("rpvc:radioReceiverFrequency", tunedFrequency ?? "");
            tree.SetBool("rpvc:radioReceiverEnabled", isEnabled);
            tree.SetInt("rpvc:radioReceiverPlaybackRange", playbackRangeBlocks);
            tree.SetString("rpvc:radioReceiverHeardStationName", heardStationName ?? "");
        }

        public override void OnBlockRemoved()
        {
            Unregister();
            base.OnBlockRemoved();
            dialog?.TryClose();
        }

        public override void OnBlockUnloaded()
        {
            Unregister();
            base.OnBlockUnloaded();
        }

        private void Unregister()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterReceiver(Pos);
                if (stationNameListenerId != -1)
                {
                    Api.Event.UnregisterGameTickListener(stationNameListenerId);
                    stationNameListenerId = -1;
                }
            }
        }
    }
}
