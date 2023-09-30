using RPVoiceChat.DB;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    public class PlayerList : GuiElementContainer, IExtendedGuiElement
    {
        private const double playerListWidth = 440;
        private const double playerListHeight = 300;
        private const double playerEntryHeight = 20;
        private const double playerEntryDeltaY = 10;
        private const double playerNameWidth = 200;
        private const double playerVolumeLeftPadding = 40;
        private const double playerVolumeWidth = 200;
        private ClientSettingsRepository _settingsRepository;
        private GuiDialog parrentDialog;
        private CairoFont font = CairoFont.WhiteSmallText();
        private ElementBounds playerEntryBounds;
        private string key;

        public PlayerList(ICoreClientAPI capi, ClientSettingsRepository settingsRepository, GuiDialog parrent) : base(capi, new ElementBounds())
        {
            _settingsRepository = settingsRepository;
            parrentDialog = parrent;

            parrentDialog.OnOpened += OnDialogOpened;
            parrentDialog.OnClosed += OnDialogClosed;
        }

        public void SetKey(string elementKey)
        {
            key = elementKey;
        }

        public void SetBounds(ElementBounds bounds)
        {
            Bounds = bounds.FlatCopy().WithFixedSize(playerListWidth, playerListHeight);
            Bounds.IsDrawingSurface = true;
        }

        public void SetupElement()
        {
            ResetElement();
            foreach (var player in api.World.AllOnlinePlayers)
                AddPlayer(player);
            RedrawElement();
        }

        private void ResetElement()
        {
            Dispose();
            Elements.Clear();
            playerEntryBounds = ElementBounds.Fixed(0, 0, 0, playerEntryHeight);
        }

        private void OnDialogOpened()
        {
            api.Event.PlayerJoin += OnPlayerJoin;
        }

        private void OnDialogClosed()
        {
            api.Event.PlayerJoin -= OnPlayerJoin;
            _settingsRepository.Save();
        }

        private void OnPlayerJoin(IPlayer player)
        {
            AddPlayer(player);
            RedrawElement();
        }

        private void RedrawElement()
        {
            parrentDialog.SingleComposer.ReCompose();
        }

        private void AddPlayer(IPlayer player)
        {
            if (parrentDialog.IsOpened() == false || key == null) return;
            var element = parrentDialog.SingleComposer.GetElement(key);
            if (element == null) return;

            string playerName = player.PlayerName;
            string playerId = player.PlayerUID;
            string sliderKey = $"{playerId}_volume";
            bool isAlreadyAdded = Elements.Exists(e => e is NamedSlider slider && slider.name == sliderKey);
            if (isAlreadyAdded) return;

            var nameBounds = playerEntryBounds.FlatCopy().WithFixedWidth(playerNameWidth);
            var volumeBounds = nameBounds.RightCopy(fixedDeltaX: playerVolumeLeftPadding).WithFixedWidth(playerVolumeWidth);
            var nameLabel = new GuiElementStaticText(api, playerName, font.Orientation, nameBounds, font);
            var volumeSlider = NamedSlider.Create(api, sliderKey, SlidePlayerVolume, volumeBounds);

            float gain = _settingsRepository.GetPlayerGain(playerId);
            volumeSlider.SetValues((int)(gain * 100), 0, 100, 1, "%");

            Add(nameLabel);
            Add(volumeSlider);

            playerEntryBounds = playerEntryBounds.BelowCopy(fixedDeltaY: playerEntryDeltaY);
        }

        private bool SlidePlayerVolume(int gain, string sliderKey)
        {
            string playerId = sliderKey.Split("_")[0];
            _settingsRepository.SetPlayerGain(playerId, gain);

            return true;
        }
    }
}
