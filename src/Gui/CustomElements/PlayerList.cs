using RPVoiceChat.DB;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    public class PlayerList : GuiElementContainer, IExtendedGuiElement
    {
        private const double playerListWidth = playerNameWidth + playerVolumeLeftPadding + playerVolumeWidth;
        private const double playerListVisibleHeight = playerEntryHeightWithOffset * maxPlayerEntriesOnScreen + _fixOOSSliderHandleClip;
        private const double playerEntryHeight = 20;
        private const double playerEntryDeltaY = 10;
        private const double playerEntryHeightWithOffset = playerEntryHeight + playerEntryDeltaY;
        private const double _playerEntriesYPadding = 7.5; // Compensates for slider handles pocking out ouf slider bounds
        private const double _playerEntriesYOffset = 2; // Compensates for slider handles pocking out ouf slider bounds (top part is longer)
        private const double maxPlayerEntriesOnScreen = 15;
        private const double playerNameWidth = 200;
        private const double playerVolumeLeftPadding = 20;
        private const double playerVolumeWidth = 200;
        private const double scrollbarYPadding = 2;
        private const double scrollbarLeftPadding = 10;
        private const double scrollbarWidth = 5;
        private const double scrollbarHeight = playerListVisibleHeight - scrollbarYPadding * 2;
        private const double _fixOOSSliderHandleClip = 2.5; // Adjusts visible height to clip right before a handle of out-of-screen slider starts
        private const double _fixHSBSliderHandleClip = 6; // Adjusts visible height to not clip handle of last slider when scroll bar is hidden
        private CairoFont font = CairoFont.WhiteSmallText();
        private ClientSettingsRepository _settingsRepository;
        private GuiDialog parrentDialog;
        private ElementBounds playerEntryBounds;
        private string key;

        public PlayerList(ICoreClientAPI capi, ClientSettingsRepository settingsRepository, GuiDialog parrent) : base(capi, new ElementBounds())
        {
            _settingsRepository = settingsRepository;
            parrentDialog = parrent;
            playerEntryBounds = ElementBounds.Fixed(0, 0, 0, playerEntryHeight);
            UnscaledCellHorPadding = 0;
            unscaledCellSpacing = 0;
            UnscaledCellVerPadding = 0;

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
