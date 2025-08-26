using RPVoiceChat.GameContent.BlockBehaviors;
using RPVoiceChat.GameContent.BlockEntities;
using RPVoiceChat.GameContent.BlockEntityBehaviors;
using RPVoiceChat.GameContent.Blocks;
using RPVoiceChat.GameContent.Items;
using RPVoiceChat.src.Networking.Packets;
using RPVoiceChat.Utils;
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

        protected RPVoiceChatConfig config;
        private PatchManager patchManager;

        public override void StartPre(ICoreAPI api)
        {
            ClientSettings.Init(api);
            ModConfig.ReadConfig(api);
            config = ModConfig.Config;
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
            }
            else if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                ServerChannel = sapi.Network.RegisterChannel("welding")
                    .RegisterMessageType<WeldingHitPacket>()
                    .SetMessageHandler<WeldingHitPacket>(OnWeldingHitReceived);
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
    }
}
