using System;
using System.Linq;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public enum MixingConsoleOnAirResult
    {
        Success,
        AlreadyOnAir,
        NotOperator,
        NotWired,
        NoBroadcastPath
    }

    public class BlockEntityRadioMixingConsole : BEWireNode, IWireTypedNode, IRadioProgramSource
    {
        private const int MaxHlsUrlLength = 2048;

        private RadioMixingConsoleDialog dialog;
        private string hlsStreamUrl = "";
        private bool isOnAir;
        private string activeOperatorPlayerUid = "";

        public override bool IsActiveEndpoint => true;
        protected override int MaxConnections => 3;
        public WireNodeKind WireNodeKind => WireNodeKind.Radio;

        public bool IsOnAir => isOnAir;
        public string HlsStreamUrl => hlsStreamUrl ?? "";
        public string ActiveOperatorPlayerUid => activeOperatorPlayerUid ?? "";

        public string ProgramRouteKey => RadioProgramRouteKey.ForMixingConsole(Pos);

        public bool IsBusyForOtherPlayer(string playerUid)
        {
            return isOnAir && !IsOperator(playerUid);
        }

        public bool HasWiredBroadcastPath()
        {
            if (NetworkUID == 0)
            {
                return false;
            }

            return RadioWireNetworkHelper.FindEmitters(this).Any()
                || RadioWireNetworkHelper.FindSpeakers(this).Any();
        }

        public bool HasActiveBroadcastOutput()
        {
            bool hasSpeaker = RadioWireNetworkHelper.FindSpeakers(this).Any();
            bool hasPoweredEmitter = RadioWireNetworkHelper.FindEmitters(this).Any(emitter => emitter.IsWirelessTransmitting);
            return hasSpeaker || hasPoweredEmitter;
        }

        public bool IsOperator(string playerUid)
        {
            if (!isOnAir || string.IsNullOrWhiteSpace(activeOperatorPlayerUid))
            {
                return true;
            }

            if (Api?.Side == EnumAppSide.Server && Api.World.PlayerByUid(activeOperatorPlayerUid) == null)
            {
                return true;
            }

            return activeOperatorPlayerUid == playerUid;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            OnConnectionsChanged += OnRadioWireConnectionsChanged;
            if (api.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.RegisterMixingConsole(Pos);
            }
        }

        private void OnRadioWireConnectionsChanged()
        {
            if (Api is ICoreServerAPI sapi)
            {
                WireTopologyConnectivity.NotifyNode(sapi, this);
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

            dialog = new RadioMixingConsoleDialog(capi, this);
            dialog.TryOpen();
            return true;
        }

        public void RequestSetHlsUrl(string desired)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetMixingConsoleHlsUrl,
                Value = desired ?? ""
            });
        }

        public void RequestSetOnAir(bool enabled)
        {
            if (Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            RPVoiceChatMod.RadioSettingsClientChannel?.SendPacket(new RadioSettingsPacket
            {
                BlockPos = Pos,
                Operation = RadioSettingsOperation.SetMixingConsoleOnAir,
                IntValue = enabled ? 1 : 0
            });
        }

        public bool SetHlsUrl(string desired)
        {
            string normalized = NormalizeHlsUrl(desired);
            if (normalized == null)
            {
                return false;
            }

            hlsStreamUrl = normalized;
            MarkDirty(true);
            dialog?.RefreshData();
            return true;
        }

        public MixingConsoleOnAirResult SetOnAir(IPlayer byPlayer, bool enabled)
        {
            if (Api?.Side != EnumAppSide.Server || byPlayer == null)
            {
                return MixingConsoleOnAirResult.NotOperator;
            }

            if (enabled)
            {
                if (isOnAir)
                {
                    return MixingConsoleOnAirResult.AlreadyOnAir;
                }

                if (NetworkUID == 0)
                {
                    return MixingConsoleOnAirResult.NotWired;
                }

                if (!HasWiredBroadcastPath())
                {
                    return MixingConsoleOnAirResult.NoBroadcastPath;
                }

                isOnAir = true;
                activeOperatorPlayerUid = byPlayer.PlayerUID;
            }
            else
            {
                if (!IsOperator(byPlayer.PlayerUID))
                {
                    return MixingConsoleOnAirResult.NotOperator;
                }

                ClearOnAirInternal();
            }

            MarkDirty(true);
            return MixingConsoleOnAirResult.Success;
        }

        public static string GetOnAirFailureLangKey(MixingConsoleOnAirResult result)
        {
            return result switch
            {
                MixingConsoleOnAirResult.AlreadyOnAir => "Radio.MixingConsole.Error.Busy",
                MixingConsoleOnAirResult.NotOperator => "Radio.MixingConsole.Error.NotOperator",
                MixingConsoleOnAirResult.NotWired => "Radio.MixingConsole.Error.NotWired",
                MixingConsoleOnAirResult.NoBroadcastPath => "Radio.MixingConsole.Error.NoBroadcastPath",
                _ => null
            };
        }

        public void ClearOnAir()
        {
            if (!isOnAir && string.IsNullOrWhiteSpace(activeOperatorPlayerUid))
            {
                return;
            }

            ClearOnAirInternal();
            MarkDirty(true);

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<RadioVoiceRoutingSystem>()?.ClearProgramRoute(ProgramRouteKey);
            }
        }

        private void ClearOnAirInternal()
        {
            isOnAir = false;
            activeOperatorPlayerUid = "";
        }

        public static string NormalizeHlsUrl(string desired)
        {
            if (desired == null)
            {
                return "";
            }

            string trimmed = desired.Trim();
            if (trimmed.Length == 0)
            {
                return "";
            }

            if (trimmed.Length > MaxHlsUrlLength)
            {
                return null;
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            return uri.AbsoluteUri;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            hlsStreamUrl = tree.GetString("rpvc:mixingConsoleHlsUrl", hlsStreamUrl);
            isOnAir = tree.GetBool("rpvc:mixingConsoleOnAir", false);
            activeOperatorPlayerUid = tree.GetString("rpvc:mixingConsoleOperatorUid", "");
            dialog?.RefreshData();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("rpvc:mixingConsoleHlsUrl", hlsStreamUrl ?? "");
            tree.SetBool("rpvc:mixingConsoleOnAir", isOnAir);
            tree.SetString("rpvc:mixingConsoleOperatorUid", activeOperatorPlayerUid ?? "");
        }

        public override void OnBlockRemoved()
        {
            ClearOnAir();
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterMixingConsole(Pos);
            }

            base.OnBlockRemoved();
            dialog?.TryClose();
        }

        public override void OnBlockUnloaded()
        {
            ClearOnAir();
            if (Api?.Side == EnumAppSide.Server)
            {
                RadioBlockIndex.UnregisterMixingConsole(Pos);
            }

            base.OnBlockUnloaded();
        }
    }
}
