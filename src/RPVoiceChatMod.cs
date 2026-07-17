using System.IO;
using System.Linq;
using System.Reflection;
using RPVoiceChat.Audio.Input;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Block;
using RPVoiceChat.GameContent.Items;
using RPVoiceChat.Gui;
using RPVoiceChat.src.Networking.Packets;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RPVoiceChat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        public static readonly string modID = "rpvoicechat";
        internal static ICoreAPI ModApi;
        internal static ICoreClientAPI capi;
        internal static ICoreServerAPI sapi;
        internal static IClientNetworkChannel ClientChannel;
        internal static IServerNetworkChannel ServerChannel;
        internal static IClientNetworkChannel TelegraphPrintClientChannel;
        internal static IServerNetworkChannel TelegraphPrintServerChannel;
        internal static IClientNetworkChannel TelegraphSettingsClientChannel;
        internal static IServerNetworkChannel TelegraphSettingsServerChannel;
        internal static IClientNetworkChannel TelephoneSettingsClientChannel;
        internal static IServerNetworkChannel TelephoneSettingsServerChannel;
        internal static IClientNetworkChannel SwitchboardClientChannel;
        internal static IServerNetworkChannel SwitchboardServerChannel;
        internal static IClientNetworkChannel AnnounceClientChannel;
        internal static IServerNetworkChannel AnnounceServerChannel;
        internal static IClientNetworkChannel RadioSettingsClientChannel;
        internal static IServerNetworkChannel RadioSettingsServerChannel;
        internal static IClientNetworkChannel RadioTalkieClientChannel;
        internal static IServerNetworkChannel RadioTalkieServerChannel;
        internal static IClientNetworkChannel NametagConfigClientChannel;
        internal static IServerNetworkChannel NametagConfigServerChannel;

        private PatchManager patchManager;

        public override void StartPre(ICoreAPI api)
        {
            ModConfig.Init(api);

            WorldConfig.Init(api);

            new Logger(api);
        }

        public override void Start(ICoreAPI api)
        {
            ModApi = api;
            string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrWhiteSpace(modFolder))
            {
                FfmpegLocator.SetModFolder(modFolder);
            }

            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
                ClientChannel = capi.Network.RegisterChannel("welding")
                    .RegisterMessageType<WeldingHitPacket>();
                    
                TelegraphPrintClientChannel = capi.Network.RegisterChannel("telegraphprint")
                    .RegisterMessageType<CommDeliveryPacket>();
                TelegraphSettingsClientChannel = capi.Network.RegisterChannel("telegraphsettings")
                    .RegisterMessageType<TelegraphSettingsPacket>();
                TelephoneSettingsClientChannel = capi.Network.RegisterChannel("telephonesettings")
                    .RegisterMessageType<TelephoneSettingsPacket>();
                RadioSettingsClientChannel = capi.Network.RegisterChannel("radiosettings")
                    .RegisterMessageType<RadioSettingsPacket>()
                    .RegisterMessageType<RadioClientNotificationPacket>();
                RadioTalkieClientChannel = capi.Network.RegisterChannel("radiotalkie")
                    .RegisterMessageType<RadioTalkieStatePacket>();
                SwitchboardClientChannel = capi.Network.RegisterChannel("switchboardsettings")
                    .RegisterMessageType<SwitchboardRenameNetworkPacket>()
                    .RegisterMessageType<SwitchboardPowerModePacket>();

                AnnounceClientChannel = capi.Network.RegisterChannel("announce")
                    .RegisterMessageType<AnnouncePacket>();
                NametagConfigClientChannel = capi.Network.RegisterChannel("rpvc-nametag-config")
                    .RegisterMessageType<NametagConfigChangedPacket>();
                PlayerNameTagRenderer.Init(capi);
            }
            else if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                ServerChannel = sapi.Network.RegisterChannel("welding")
                    .RegisterMessageType<WeldingHitPacket>()
                    .SetMessageHandler<WeldingHitPacket>(OnWeldingHitReceived);
                    
                TelegraphPrintServerChannel = sapi.Network.RegisterChannel("telegraphprint")
                    .RegisterMessageType<CommDeliveryPacket>()
                    .SetMessageHandler<CommDeliveryPacket>(OnCommDeliveryPacket);
                TelegraphSettingsServerChannel = sapi.Network.RegisterChannel("telegraphsettings")
                    .RegisterMessageType<TelegraphSettingsPacket>()
                    .SetMessageHandler<TelegraphSettingsPacket>(OnTelegraphSettingsPacket);
                TelephoneSettingsServerChannel = sapi.Network.RegisterChannel("telephonesettings")
                    .RegisterMessageType<TelephoneSettingsPacket>()
                    .SetMessageHandler<TelephoneSettingsPacket>(OnTelephoneSettingsPacket);
                RadioSettingsServerChannel = sapi.Network.RegisterChannel("radiosettings")
                    .RegisterMessageType<RadioSettingsPacket>()
                    .RegisterMessageType<RadioClientNotificationPacket>()
                    .SetMessageHandler<RadioSettingsPacket>(OnRadioSettingsPacket);
                RadioTalkieServerChannel = sapi.Network.RegisterChannel("radiotalkie")
                    .RegisterMessageType<RadioTalkieStatePacket>()
                    .SetMessageHandler<RadioTalkieStatePacket>(OnRadioTalkieStatePacket);
                SwitchboardServerChannel = sapi.Network.RegisterChannel("switchboardsettings")
                    .RegisterMessageType<SwitchboardRenameNetworkPacket>()
                    .RegisterMessageType<SwitchboardPowerModePacket>()
                    .SetMessageHandler<SwitchboardRenameNetworkPacket>(OnSwitchboardRenameNetworkPacket)
                    .SetMessageHandler<SwitchboardPowerModePacket>(OnSwitchboardPowerModePacket);

                AnnounceServerChannel = sapi.Network.RegisterChannel("announce")
                    .RegisterMessageType<AnnouncePacket>();
                NametagConfigServerChannel = sapi.Network.RegisterChannel("rpvc-nametag-config")
                    .RegisterMessageType<NametagConfigChangedPacket>();
            }

            patchManager = new PatchManager(modID);
            patchManager.Patch(api);

            ItemRegistry.RegisterItems(api);
            BlockRegistry.RegisterBlocks(api);
            BlockEntityRegistry.RegisterBlockEntities(api);
            BlockBehaviorRegistry.RegisterBlockEntityBehaviors(api);
            BlockEntityBehaviorRegistry.RegisterBlockEntityBehaviors(api);
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);

            if (api.Side == EnumAppSide.Server)
            {
                BlockBehaviorRegistry.AddBehaviors(api);
                BlockEntityBehaviorRegistry.AddBlockEntityBehaviors(api);
            }
        }

        public override void Dispose()
        {
            patchManager?.Dispose();
        }

        private void OnWeldingHitReceived(IPlayer fromPlayer, WeldingHitPacket packet)
        {
            var be = fromPlayer.Entity.World.BlockAccessor.GetBlockEntity(packet.Pos) as BEWeldable;
            if (be != null)
            {
                be.OnHammerHitOver(fromPlayer, packet.HitPosition);
            }
        }

        private void OnCommDeliveryPacket(IServerPlayer player, CommDeliveryPacket packet)
        {
            if (packet == null || packet.PayloadType != CommPayloadType.Text || string.IsNullOrEmpty(packet.TextMessage))
            {
                return;
            }

            var telegraph = sapi.World.BlockAccessor.GetBlockEntity(packet.DevicePos) as BlockEntityTelegraph;
            if (telegraph != null)
            {
                telegraph.ProcessPrintPacket(packet.TextMessage, packet.SourceEndpointName, packet.TargetEndpointName, packet.NetworkName);
            }
            else
            {
                sapi.Logger.Warning($"Telegraph at {packet.DevicePos} not found for comm delivery packet");
            }
        }

        private void OnTelegraphSettingsPacket(IServerPlayer player, TelegraphSettingsPacket packet)
        {
            var telegraph = sapi.World.BlockAccessor.GetBlockEntity(packet.TelegraphPos) as BlockEntityTelegraph;
            if (telegraph == null)
            {
                sapi.Logger.Warning($"Telegraph at {packet.TelegraphPos} not found for settings packet");
                return;
            }

            switch (packet.Operation)
            {
                case TelegraphSettingsOperation.SetCustomName:
                    telegraph.SetCustomEndpointName(packet.Value, out _);
                    break;
                case TelegraphSettingsOperation.SetTarget:
                    telegraph.SetTargetEndpointName(packet.Value);
                    break;
            }
        }

        private void OnSwitchboardRenameNetworkPacket(IServerPlayer player, SwitchboardRenameNetworkPacket packet)
        {
            var switchboard = sapi.World.BlockAccessor.GetBlockEntity(packet.SwitchboardPos) as BlockEntitySwitchboard;
            if (switchboard == null)
            {
                sapi.Logger.Warning($"Switchboard at {packet.SwitchboardPos} not found for rename packet");
                return;
            }

            switchboard.RenameNetwork(packet.NetworkName, out _);
        }

        private void OnTelephoneSettingsPacket(IServerPlayer player, TelephoneSettingsPacket packet)
        {
            var telephone = sapi.World.BlockAccessor.GetBlockEntity(packet.TelephonePos) as BlockEntityTelephone;
            if (telephone == null)
            {
                sapi.Logger.Warning($"Telephone at {packet.TelephonePos} not found for settings packet");
                return;
            }

            switch (packet.Operation)
            {
                case TelephoneSettingsOperation.SetNumber:
                    telephone.SetPhoneNumber(packet.Value);
                    break;
                case TelephoneSettingsOperation.SetTarget:
                    telephone.SetTargetNumber(packet.Value);
                    break;
                case TelephoneSettingsOperation.StartCall:
                    telephone.StartCall(player, packet.Value);
                    break;
                case TelephoneSettingsOperation.EndCall:
                    telephone.EndCall();
                    break;
            }
        }

        internal static void SendRadioClientNotification(IServerPlayer player, string langKey)
        {
            if (player == null || string.IsNullOrWhiteSpace(langKey))
            {
                return;
            }

            RadioSettingsServerChannel?.SendPacket(new RadioClientNotificationPacket
            {
                LangKey = langKey
            }, player);
        }

        private void OnRadioSettingsPacket(IServerPlayer player, RadioSettingsPacket packet)
        {
            if (packet?.BlockPos == null)
            {
                return;
            }

            switch (packet.Operation)
            {
                case RadioSettingsOperation.SetFrequency:
                case RadioSettingsOperation.SetDisplayName:
                    if (sapi.World.BlockAccessor.GetBlockEntity(packet.BlockPos) is BlockEntityRadioSupervisionConsole console)
                    {
                        if (packet.Operation == RadioSettingsOperation.SetFrequency)
                        {
                            console.SetFrequency(packet.Value);
                        }
                        else
                        {
                            console.SetDisplayName(packet.Value);
                        }
                    }
                    break;
                case RadioSettingsOperation.SetEmitterMode:
                    if (sapi.World.BlockAccessor.GetBlockEntity(packet.BlockPos) is BlockEntityRadioEmitter emitter)
                    {
                        emitter.SetOperatingMode((RadioEmitterOperatingMode)packet.IntValue);
                    }
                    break;
                case RadioSettingsOperation.SetRepeaterFrequency:
                    if (sapi.World.BlockAccessor.GetBlockEntity(packet.BlockPos) is BlockEntityRadioEmitter repeaterEmitter)
                    {
                        repeaterEmitter.SetRepeaterFrequency(packet.Value);
                    }
                    break;
                case RadioSettingsOperation.SetReceiverFrequency:
                    if (sapi.World.BlockAccessor.GetBlockEntity(packet.BlockPos) is BlockEntityRadioReceiver receiver)
                    {
                        receiver.SetTunedFrequency(packet.Value);
                    }
                    break;
                case RadioSettingsOperation.SetMicrophoneTransmit:
                    if (sapi.World.BlockAccessor.GetBlockEntity(packet.BlockPos) is BlockEntityRadioMicrophone microphone)
                    {
                        microphone.SetTransmitting(player, packet.IntValue != 0);
                    }
                    break;
                case RadioSettingsOperation.SetMixingConsoleHlsUrl:
                    if (sapi.World.BlockAccessor.GetBlockEntity(packet.BlockPos) is BlockEntityRadioMixingConsole mixingConsoleUrl)
                    {
                        mixingConsoleUrl.SetHlsUrl(packet.Value);
                    }
                    break;
                case RadioSettingsOperation.SetMixingConsoleOnAir:
                    if (sapi.World.BlockAccessor.GetBlockEntity(packet.BlockPos) is BlockEntityRadioMixingConsole mixingConsoleAir)
                    {
                        MixingConsoleOnAirResult result = mixingConsoleAir.SetOnAir(player, packet.IntValue != 0);
                        string failureLangKey = BlockEntityRadioMixingConsole.GetOnAirFailureLangKey(result);
                        if (!string.IsNullOrWhiteSpace(failureLangKey))
                        {
                            SendRadioClientNotification(player, failureLangKey);
                        }
                    }
                    break;
            }
        }

        private void OnRadioTalkieStatePacket(IServerPlayer player, RadioTalkieStatePacket packet)
        {
            sapi.ModLoader.GetModSystem<RadioTalkieTransmissionSystem>()
                ?.SetTalkieTransmitting(player, packet?.Transmitting == true, packet?.Frequency);
        }

        private void OnSwitchboardPowerModePacket(IServerPlayer player, SwitchboardPowerModePacket packet)
        {
            var switchboard = sapi.World.BlockAccessor.GetBlockEntity(packet.SwitchboardPos) as BlockEntitySwitchboard;
            if (switchboard == null)
            {
                sapi.Logger.Warning($"Switchboard at {packet.SwitchboardPos} not found for power-mode packet");
                return;
            }

            switchboard.SetUsePowerRequirements(packet.UsePowerRequirements);
        }

    }
}

