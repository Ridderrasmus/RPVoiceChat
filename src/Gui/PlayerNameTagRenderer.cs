using RPVoiceChat.Audio;
using RPVoiceChat.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.Gui
{
    public class PlayerNameTagRenderer
    {
        /// <summary>RPVC fallback value for <c>renderRange</c> in the nametag tree.</summary>
        public const int DefaultNametagRenderRange = 99;

        private static ICoreClientAPI capi;
        private static AudioOutputManager _audioOutputManager;

        public PlayerNameTagRenderer(ICoreClientAPI api, AudioOutputManager audioOutputManager)
        {
            capi = api;
            _audioOutputManager = audioOutputManager;
        }

        public static LoadedTexture GetRenderer(EntityPlayer entity, double[] color = null, TextBackground bg = null)
        {
            string playerName = entity?.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (playerName == null || playerName.Length == 0) return null;

            var nametagAttribute = entity.WatchedAttributes?.GetTreeAttribute("nametag");
            if (nametagAttribute == null) return null;

            string playerUID = entity.PlayerUID;
            bool isTalking = _audioOutputManager?.IsPlayerTalking(playerUID) == true;
            if (ApplyNametagVisibilitySettings(entity, nametagAttribute, isTalking))
            {
                entity.WatchedAttributes.MarkPathDirty("nametag");
            }

            color ??= ColorUtil.WhiteArgbDouble;
            var textColor = CairoFont.WhiteMediumText().WithColor(color);
            var textBg = bg?.Clone() ?? new TextBackground()
            {
                FillColor = GuiStyle.DialogLightBgColor,
                Padding = 3,
                Radius = GuiStyle.ElementBGRadius,
                Shade = true,
                BorderColor = GuiStyle.DialogBorderColor,
                BorderWidth = 3,
            };

            if (isTalking)
            {
                textBg.FillColor = ColorUtil.Hex2Doubles("#529F51", 0.75);
                textBg.BorderColor = new double[4] { 0.0, 1, 0.0, 0.3 };
                textBg.BorderWidth = 5;
            }

            return capi.Gui.TextTexture.GenUnscaledTextTexture(playerName, textColor, textBg);
        }

        private static bool ApplyNametagVisibilitySettings(EntityPlayer entity, ITreeAttribute nametagAttribute, bool isTalking)
        {
            if (entity == null || nametagAttribute == null) return false;

            bool forceRender = WorldConfig.GetForceSpeakerNametag();
            bool nameTagsEnabled = !WorldConfig.GetPlayerNametagTargetedOnly();
            bool shouldRender = nameTagsEnabled || (isTalking && forceRender);
            bool targetOnly = !shouldRender;
            if (nametagAttribute.GetBool("showtagonlywhentargeted") == targetOnly) return false;

            nametagAttribute.SetBool("showtagonlywhentargeted", targetOnly);
            return true;
        }

        public static void SetNametagRenderRange(IPlayer player, int renderRange)
        {
            if (capi == null) return;
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (player?.Entity == null) return;
                ITreeAttribute nametagAttribute = player.Entity.WatchedAttributes.GetTreeAttribute("nametag");
                if (nametagAttribute == null) return;
                if (nametagAttribute.GetInt("renderRange") == renderRange) return;
                nametagAttribute.SetInt("renderRange", renderRange);
                player.Entity.WatchedAttributes.MarkPathDirty("nametag");
            }, "rpvoicechat:SetNametagRenderRange");
        }

        public static void UpdatePlayerNameTag(IPlayer player, bool isTalking)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (player?.Entity is not EntityPlayer entityPlayer) return;
                var nametagAttribute = entityPlayer.WatchedAttributes?.GetTreeAttribute("nametag");
                if (nametagAttribute == null) return;

                ApplyNametagVisibilitySettings(entityPlayer, nametagAttribute, isTalking);
                // Force a redraw on every speaking-state event so the color toggles reliably.
                entityPlayer.WatchedAttributes.MarkPathDirty("nametag");
            }, "rpvoicechat:UpdateNameTag");
        }

        /// <summary>
        /// Cleans up cached textures for a specific player (call when player disconnects)
        /// </summary>
        public static void CleanupPlayerCache(string playerUID)
        {
            // No-op: nametag texture lifecycle is owned by the game renderer.
        }

        /// <summary>
        /// Cleans up all cached textures (call when mod is disposed)
        /// </summary>
        public static void CleanupAllCache()
        {
            // No-op: nametag texture lifecycle is owned by the game renderer.
        }
    }
}
