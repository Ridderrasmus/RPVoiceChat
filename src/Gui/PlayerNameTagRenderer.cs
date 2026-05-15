using RPVoiceChat.Audio;
using RPVoiceChat.Config;
using System.Collections.Generic;
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
        private static readonly Dictionary<string, bool> lastTalkingStateByPlayer = new Dictionary<string, bool>();

        public PlayerNameTagRenderer(ICoreClientAPI api, AudioOutputManager audioOutputManager)
        {
            capi = api;
            _audioOutputManager = audioOutputManager;
        }

        public static LoadedTexture GetRenderer(EntityPlayer entity, double[] color = null, TextBackground bg = null)
        {
            string playerName = entity?.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (playerName == null || playerName.Length == 0) return null;

            if (entity.WatchedAttributes?.GetTreeAttribute("nametag") == null) return null;

            bool isTalking = _audioOutputManager?.IsPlayerTalking(entity.PlayerUID) == true;

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

            // Vanilla disposes the previous texture in OnNameChanged before assigning the new one.
            return capi.Gui.TextTexture.GenUnscaledTextTexture(playerName, textColor, textBg);
        }

        private static bool ApplyNametagVisibilitySettings(ITreeAttribute nametagAttribute, bool isTalking)
        {
            if (nametagAttribute == null) return false;

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
            if (capi == null || player?.Entity == null) return;

            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (player?.Entity is not EntityPlayer entityPlayer) return;

                string playerUID = entityPlayer.PlayerUID;
                bool talkStateChanged = !lastTalkingStateByPlayer.TryGetValue(playerUID, out bool wasTalking) || wasTalking != isTalking;

                var nametagAttribute = entityPlayer.WatchedAttributes?.GetTreeAttribute("nametag");
                if (nametagAttribute == null) return;

                bool visibilityChanged = ApplyNametagVisibilitySettings(nametagAttribute, isTalking);

                if (!talkStateChanged && !visibilityChanged) return;

                lastTalkingStateByPlayer[playerUID] = isTalking;
                entityPlayer.WatchedAttributes.MarkPathDirty("nametag");
            }, "rpvoicechat:UpdatePlayerNameTag");
        }

        public static void RefreshAllPlayerNameTags()
        {
            if (capi?.World == null) return;

            lastTalkingStateByPlayer.Clear();

            capi.Event.EnqueueMainThreadTask(() =>
            {
                foreach (IPlayer player in capi.World.AllOnlinePlayers)
                {
                    if (player?.Entity is not EntityPlayer entityPlayer) continue;

                    bool isTalking = _audioOutputManager?.IsPlayerTalking(player.PlayerUID) == true;
                    var nametagAttribute = entityPlayer.WatchedAttributes?.GetTreeAttribute("nametag");
                    if (nametagAttribute == null) continue;

                    ApplyNametagVisibilitySettings(nametagAttribute, isTalking);
                    lastTalkingStateByPlayer[player.PlayerUID] = isTalking;
                    entityPlayer.WatchedAttributes.MarkPathDirty("nametag");
                }
            }, "rpvoicechat:RefreshAllPlayerNameTags");
        }

        public static void CleanupPlayerNametagCache(string playerUID)
        {
            if (string.IsNullOrEmpty(playerUID)) return;
            lastTalkingStateByPlayer.Remove(playerUID);
        }

        public static void CleanupAllNametagCache()
        {
            lastTalkingStateByPlayer.Clear();
        }
    }
}
