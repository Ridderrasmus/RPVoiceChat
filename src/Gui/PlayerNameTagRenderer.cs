using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using RPVoiceChat.Audio;

namespace RPVoiceChat
{
    public class PlayerNameTagRenderer
    {
        private static ICoreClientAPI capi;
        private static AudioOutputManager _audioOutputManager;
        private static bool? defaultShowTagOnlyWhenTargeted;

        public PlayerNameTagRenderer(ICoreClientAPI api, AudioOutputManager audioOutputManager)
        {
            capi = api;
            _audioOutputManager = audioOutputManager;
        }

        public static LoadedTexture GetRenderer(EntityPlayer entity, double[] color = null, TextBackground bg = null)
        {
            string playerName = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (playerName == null || playerName.Length == 0) return null;

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

            bool isTalking = _audioOutputManager.IsPlayerTalking(entity.PlayerUID);
            if (isTalking)
            {
                textBg.FillColor = ColorUtil.Hex2Doubles("#529F51", 0.75);
                textBg.BorderColor = new double[4] { 0.0, 1, 0.0, 0.3 };
                textBg.BorderWidth = 5;
            }

            return capi.Gui.TextTexture.GenUnscaledTextTexture(playerName, textColor, textBg);
        }

        public static void UpdatePlayerNameTag(IPlayer player, bool forceDisplay)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (player?.Entity == null) return;
                var playerAttributes = player.Entity.WatchedAttributes;
                var nametagAttribute = playerAttributes.GetTreeAttribute("nametag");
                if (defaultShowTagOnlyWhenTargeted == null) defaultShowTagOnlyWhenTargeted = nametagAttribute.GetBool("showtagonlywhentargeted");

                var showTagOnlyWhenTargeted = forceDisplay ? false : defaultShowTagOnlyWhenTargeted;
                nametagAttribute.SetBool("showtagonlywhentargeted", (bool)showTagOnlyWhenTargeted);
                playerAttributes.MarkPathDirty("nametag");
            }, "rpvoicechat:UpdateNameTag");
        }
    }
}
