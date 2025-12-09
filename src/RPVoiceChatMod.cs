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

            // Recipes will be filtered in AssetsFinalize
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);

            // Disable items/blocks based on configuration
            DisableContentBasedOnConfig(api);

            if (api.Side == EnumAppSide.Server)
            {
                BlockBehaviorRegistry.AddBehaviors(api);
                BlockEntityBehaviorRegistry.AddBlockEntityBehaviors(api);
            }
        }

        private void DisableContentBasedOnConfig(ICoreAPI api)
        {
            bool additionalContentEnabled = WorldConfig.GetBool("additional-content");
            bool telegraphContentEnabled = WorldConfig.GetBool("telegraph-content");

            // Build lists of codes to disable
            var disabledItems = new System.Collections.Generic.List<string>();
            var disabledBlocks = new System.Collections.Generic.List<string>();

            if (!additionalContentEnabled)
            {
                // Additional content items
                disabledItems.AddRange(new[]
                {
                    $"{modID}:handbell",
                    $"{modID}:royalhorn",
                    $"{modID}:warhorn",
                    $"{modID}:megaphone",
                    $"{modID}:enhancedmegaphone",
                    $"{modID}:handheldradio",
                    $"{modID}:radiocase",
                    // Resource items
                    $"{modID}:smallbellparts",
                    $"{modID}:royalhornhead",
                    $"{modID}:royalhornhandle",
                    $"{modID}:perfectgoathorn"
                });

                // Additional content blocks
                disabledBlocks.AddRange(new[]
                {
                    $"{modID}:callbell",
                    $"{modID}:carillonbell",
                    $"{modID}:churchbell",
                    $"{modID}:churchbell-part",
                    $"{modID}:churchbell-layer",
                    // Mold blocks
                    $"{modID}:smallbellpartsmold",
                    $"{modID}:royalhornheadmold",
                    $"{modID}:carillonbellmold",
                    $"{modID}:churchbellmold"
                });
            }

            if (!telegraphContentEnabled)
            {
                // Telegraph content items
                disabledItems.AddRange(new[]
                {
                    $"{modID}:telegraphwire",
                    $"{modID}:telegram",
                    $"{modID}:paperslip"
                });

                // Telegraph content blocks
                disabledBlocks.AddRange(new[]
                {
                    $"{modID}:telegraph",
                    $"{modID}:connector",
                    $"{modID}:printer"
                });
            }

            // Disable ITEMS
            foreach (Item item in api.World.Items)
            {
                if (item?.Code == null) continue;

                // Check if item is from our mod
                if (!item.Code.Domain.Equals(modID, System.StringComparison.OrdinalIgnoreCase)) continue;

                string itemCode = item.Code.ToString();
                string itemPath = item.Code.Path;

                // Check if exact code is in list or if path starts with a disabled prefix
                bool shouldDisable = disabledItems.Contains(itemCode) ||
                    disabledItems.Any(disabledCode => itemPath.StartsWith(disabledCode.Replace($"{modID}:", ""), System.StringComparison.OrdinalIgnoreCase));

                if (shouldDisable)
                {
                    item.IsMissing = true;
                    item.CreativeInventoryTabs = null;
                    api.Logger.Notification($"[RPVoiceChat] Item disabled: {itemCode}");
                }
            }

            // Disable BLOCKS (with variant support)
            foreach (Block block in api.World.Blocks)
            {
                if (block?.Code == null) continue;

                // Check if block is from our mod
                if (!block.Code.Domain.Equals(modID, System.StringComparison.OrdinalIgnoreCase)) continue;

                string blockCode = block.Code.ToString();
                string blockPath = block.Code.Path;

                // Check if exact code is in list or if path starts with a disabled prefix
                bool shouldDisable = disabledBlocks.Contains(blockCode) ||
                    disabledBlocks.Any(disabledCode => blockPath.StartsWith(disabledCode.Replace($"{modID}:", ""), System.StringComparison.OrdinalIgnoreCase));

                if (shouldDisable)
                {
                    block.IsMissing = true;
                    block.CreativeInventoryTabs = null;
                    api.Logger.Notification($"[RPVoiceChat] Block disabled: {blockCode}");
                }
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