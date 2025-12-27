using System.Linq;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Block;
using RPVoiceChat.GameContent.Items;
using RPVoiceChat.src.Networking.Packets;
using RPVoiceChat.Networking.Packets;
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

            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
                ClientChannel = capi.Network.RegisterChannel("welding")
                    .RegisterMessageType<WeldingHitPacket>();
                    
                TelegraphPrintClientChannel = capi.Network.RegisterChannel("telegraphprint")
                    .RegisterMessageType<TelegraphPrintPacket>();
            }
            else if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                ServerChannel = sapi.Network.RegisterChannel("welding")
                    .RegisterMessageType<WeldingHitPacket>()
                    .SetMessageHandler<WeldingHitPacket>(OnWeldingHitReceived);
                    
                TelegraphPrintServerChannel = sapi.Network.RegisterChannel("telegraphprint")
                    .RegisterMessageType<TelegraphPrintPacket>()
                    .SetMessageHandler<TelegraphPrintPacket>(OnTelegraphPrintPacket);
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

        private void OnTelegraphPrintPacket(IServerPlayer player, TelegraphPrintPacket packet)
        {
            // Find the telegraph block entity
            var telegraph = sapi.World.BlockAccessor.GetBlockEntity(packet.TelegraphPos) as BlockEntityTelegraph;
            if (telegraph != null)
            {
                telegraph.ProcessPrintPacket(packet.Message);
            }
            else
            {
                sapi.Logger.Warning($"Telegraph at {packet.TelegraphPos} not found for print packet");
            }
        }
    }
}