using RPVoiceChat.DB;
using RPVoiceChat.Utils;
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
        private ClientSettingsRepository settingsRepository;
        private GuiDialog parrentDialog;
        private ElementBounds playerEntryBounds;
        private GuiElementScrollbar scrollbar;
        private string key;
        private ElementBounds _clipBounds;

        public PlayerList(ICoreClientAPI capi, ClientSettingsRepository clientSettingsRepository, GuiDialog parrent) : base(capi, new ElementBounds())
        {
            settingsRepository = clientSettingsRepository;
            parrentDialog = parrent;
            playerEntryBounds = ElementBounds.Fixed(0, 0, 0, playerEntryHeight);
            UnscaledCellHorPadding = 0;
            unscaledCellSpacing = 0;
            UnscaledCellVerPadding = 0;

            parrentDialog.OnOpened += OnDialogOpened;
            parrentDialog.OnClosed += OnDialogClosed;
        }

        public void Init(string elementKey, ElementBounds bounds, GuiComposer composer)
        {
            key = elementKey;
            Bounds = ElementBounds.Fixed(0, 0, playerListWidth, 0).WithFixedPadding(0, _playerEntriesYPadding);
            Bounds.IsDrawingSurface = true;

            _clipBounds = bounds.FlatCopy().WithFixedSize(playerListWidth, playerListVisibleHeight).FixedGrow(scrollbarLeftPadding, 0);
            composer.BeginClip(_clipBounds.FlatCopy());
        }

        public void OnAdd(GuiComposer composer)
        {
            composer.EndClip();
            scrollbar = CreateScrollbar();
            composer.AddInteractiveElement(scrollbar);
        }

        public void SetupElement()
        {
            if (IsDisplayed() == false) return;
            ResetElement();
            foreach (var player in api.World.AllOnlinePlayers)
                AddPlayer(player);
            UpdateScrollbarHeights();
            RedrawElement();
        }

        private void ResetElement()
        {
            Dispose();
            Elements.Clear();
            playerEntryBounds = ElementBounds.Fixed(0, 0, 0, playerEntryHeight);
        }

        private void RedrawElement()
        {
            HideScrollbar();
            var playerEntryCount = Elements.Count(e => e is NamedSlider);
            if (playerEntryCount > maxPlayerEntriesOnScreen)
                ShowScrollbar();

            parrentDialog.SingleComposer.ReCompose();
        }

        private void AddPlayer(IPlayer player)
        {
            string playerName = UIUtils.TrimText(player.PlayerName, playerNameWidth, font);
            string playerId = player.PlayerUID;
            string sliderKey = $"{playerId}_volume";
            bool isAlreadyAdded = Elements.Exists(e => e is NamedSlider slider && slider.name == sliderKey);
            if (isAlreadyAdded) return;

            var nameBounds = playerEntryBounds.FlatCopy().WithFixedWidth(playerNameWidth).WithFixedOffset(0, _playerEntriesYOffset);
            var volumeBounds = nameBounds.RightCopy(fixedDeltaX: playerVolumeLeftPadding).WithFixedWidth(playerVolumeWidth);
            var nameLabel = new GuiElementStaticText(api, playerName, font.Orientation, nameBounds, font);
            var volumeSlider = new NamedSlider(api, sliderKey, SlidePlayerVolume, volumeBounds);

            float gain = settingsRepository.GetPlayerGain(playerId);
            volumeSlider.SetValues((int)(gain * 100), 0, 100, 1, "%");

            Add(nameLabel);
            Add(volumeSlider);
            playerEntryBounds = playerEntryBounds.BelowCopy(fixedDeltaY: playerEntryDeltaY);
        }

        private bool SlidePlayerVolume(int gain, string sliderKey)
        {
            string playerId = sliderKey.Split('_')[0];
            settingsRepository.SetPlayerGain(playerId, gain);

            return true;
        }

        private GuiElementScrollbar CreateScrollbar()
        {
            var scrollbarBounds = InsideClipBounds
                .RightCopy()
                .WithParent(null)
                .WithFixedSize(scrollbarWidth, scrollbarHeight)
                .WithFixedPadding(0, scrollbarYPadding);

            return new GuiElementScrollbar(api, OnNewScrollbarValue, scrollbarBounds);
        }

        private void OnNewScrollbarValue(float value)
        {
            Bounds.fixedY = 0 - value;
            Bounds.CalcWorldBounds();
        }

        private void UpdateScrollbarHeights()
        {
            if (scrollbar == null) return;
            scrollbar.Bounds.CalcWorldBounds();
            var playerEntryCount = Elements.Count(e => e is NamedSlider);
            var visibleHeight = (int)playerListVisibleHeight;
            var totalHeight = (int)(playerEntryHeightWithOffset * playerEntryCount + _playerEntriesYPadding);
            scrollbar.SetHeights(visibleHeight, totalHeight);
        }

        private void HideScrollbar()
        {
            InsideClipBounds.WithFixedSize(
                _clipBounds.fixedWidth + scrollbar.Bounds.OuterWidth,
                _clipBounds.fixedHeight + _fixHSBSliderHandleClip
            );
            scrollbar.Bounds.fixedY = -100000;

            InsideClipBounds.WithFixedPadding(0, 0);
            Bounds.fixedOffsetY = 0;
        }

        private void ShowScrollbar()
        {
            InsideClipBounds.WithFixedSize(_clipBounds.fixedWidth, _clipBounds.fixedHeight);
            scrollbar.Bounds.fixedY = _clipBounds.fixedY;

            InsideClipBounds.WithFixedPadding(0, _fixHSBSliderHandleClip / 2);
            Bounds.fixedOffsetY = -_fixHSBSliderHandleClip / 2;
        }

        private void OnDialogOpened()
        {
            api.Event.PlayerJoin += OnPlayerJoin;
        }

        private void OnDialogClosed()
        {
            api.Event.PlayerJoin -= OnPlayerJoin;
            settingsRepository.Save();
        }

        private void OnPlayerJoin(IPlayer player)
        {
            if (IsDisplayed() == false) return;
            AddPlayer(player);
            UpdateScrollbarHeights();
            RedrawElement();
        }

        private bool IsDisplayed()
        {
            if (parrentDialog.IsOpened() == false || key == null) return false;
            var element = parrentDialog.SingleComposer.GetElement(key);
            if (element == null) return false;

            return true;
        }
    }
}
