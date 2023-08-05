using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace rpvoicechat
{
    public class HudIcon : HudElement
    {
        MicrophoneManager _microphoneManager;
        RPVoiceChatConfig _config;

        string voice;
        long listenerId;

        float size = 64;

        public HudIcon(ICoreClientAPI capi, MicrophoneManager microphoneManager) : base(capi)
        {
            _microphoneManager = microphoneManager;
            _config = ModConfig.Config;
        }

        public override void OnOwnPlayerDataReceived()
        {
            voice = capi.World.Player?.Entity.talkUtil.soundName.GetName();

            this.ComposeGuis();

            listenerId = capi.Event.RegisterGameTickListener(OnGameTick, 20);
        }

        private void OnGameTick(float obj)
        {
            voice = capi?.World.Player.Entity.talkUtil.soundName.GetName();
            
            this.ComposeGuis();
        }

        public void ComposeGuis()
        {

            ElementBounds dialogBounds = new ElementBounds()
            {
                Alignment = EnumDialogArea.RightBottom,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = size,
                fixedHeight = size,
                fixedPaddingX = 10,
                fixedPaddingY = 10
            };

            var composer = this.capi.Gui.CreateCompo("rpvcicon", dialogBounds);

            if (_microphoneManager.IsTalking || _config.IsMuted)
            {
                composer.AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation("rpvoicechat", "textures/gui/" + voice + ".png"));
            }
            if (_config.IsMuted)
            {
                composer.AddImage(ElementBounds.Fixed(0, 0, size, size), new AssetLocation("rpvoicechat", "textures/gui/muted.png"));
            }

            composer.Compose();
            this.Composers["rpvcicon"] = composer;
            this.TryOpen();
        }

        

        public override void Dispose()
        {
            base.Dispose();

            capi.Event.UnregisterGameTickListener(listenerId);
        }
    }
}
