using RPVoiceChat.Audio;
using RPVoiceChat.Config;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.Gui
{
    public class PlayerNameTagRenderer
    {
        private static ICoreClientAPI capi;
        private static AudioOutputManager _audioOutputManager;
        private static bool? defaultShowTagOnlyWhenTargeted;
        
        // Cache for name tag textures to avoid recreating them every frame
        private static Dictionary<string, LoadedTexture> nameTagCache = new Dictionary<string, LoadedTexture>();
        private static Dictionary<string, bool> lastTalkingState = new Dictionary<string, bool>();

        public PlayerNameTagRenderer(ICoreClientAPI api, AudioOutputManager audioOutputManager)
        {
            capi = api;
            _audioOutputManager = audioOutputManager;
        }

        public static LoadedTexture GetRenderer(EntityPlayer entity, double[] color = null, TextBackground bg = null)
        {
            string playerName = entity?.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (playerName == null || playerName.Length == 0) return null;

            string playerUID = entity.PlayerUID;
            bool isTalking = _audioOutputManager.IsPlayerTalking(playerUID);
            
            // Check if we need to recreate the texture (player state changed)
            if (nameTagCache.ContainsKey(playerUID) && lastTalkingState.ContainsKey(playerUID))
            {
                if (lastTalkingState[playerUID] == isTalking)
                {
                    return nameTagCache[playerUID]; // Return cached texture
                }
                else
                {
                    // Player state changed, dispose old texture and recreate
                    nameTagCache[playerUID]?.Dispose();
                    nameTagCache.Remove(playerUID);
                }
            }

            // Create new texture
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

            var texture = capi.Gui.TextTexture.GenUnscaledTextTexture(playerName, textColor, textBg);
            
            // Cache the texture
            nameTagCache[playerUID] = texture;
            lastTalkingState[playerUID] = isTalking;
            
            return texture;
        }

        public static void UpdatePlayerNameTag(IPlayer player, bool isTalking)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (player?.Entity == null) return;
                var playerAttributes = player.Entity.WatchedAttributes;
                var nametagAttribute = playerAttributes.GetTreeAttribute("nametag");
                if (defaultShowTagOnlyWhenTargeted == null) defaultShowTagOnlyWhenTargeted = nametagAttribute.GetBool("showtagonlywhentargeted");

                bool forceRender = WorldConfig.GetBool("force-render-name-tags");
                bool nameTagsEnabled = !(bool)defaultShowTagOnlyWhenTargeted;
                bool shouldRender = nameTagsEnabled || (isTalking && forceRender);
                nametagAttribute.SetBool("showtagonlywhentargeted", !shouldRender);

                playerAttributes.MarkPathDirty("nametag");
            }, "rpvoicechat:UpdateNameTag");
        }

        /// <summary>
        /// Cleans up cached textures for a specific player (call when player disconnects)
        /// </summary>
        public static void CleanupPlayerCache(string playerUID)
        {
            if (nameTagCache.ContainsKey(playerUID))
            {
                nameTagCache[playerUID]?.Dispose();
                nameTagCache.Remove(playerUID);
                lastTalkingState.Remove(playerUID);
            }
        }

        /// <summary>
        /// Cleans up all cached textures (call when mod is disposed)
        /// </summary>
        public static void CleanupAllCache()
        {
            foreach (var texture in nameTagCache.Values)
            {
                texture?.Dispose();
            }
            nameTagCache.Clear();
            lastTalkingState.Clear();
        }
    }
}
