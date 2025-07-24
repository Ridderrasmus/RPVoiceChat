using RPVoiceChat.Audio;
using RPVoiceChat.Gui.CustomElements;
using RPVoiceChat.VoiceGroups.Manager;
using System;
using System.Collections.Generic;
using System.Drawing;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using RPVoiceChat.DB;

namespace RPVoiceChat.Gui
{
    public class GroupDisplay : HudElement
    {
        private VoiceGroupManagerClient _voiceGroupManager;
        private AudioOutputManager _audioOutputManager;

        private const float size = 200;
        private ElementBounds dialogBounds = new ElementBounds()
        {
            Alignment = EnumDialogArea.RightBottom,
            BothSizing = ElementSizing.Fixed,
            fixedHeight = size,
            fixedWidth = size,
            fixedPaddingX = 10,
            fixedPaddingY = 10 * 2 + size
        };

        public GroupDisplay(ICoreClientAPI capi, VoiceGroupManagerClient voiceGroupManager, AudioOutputManager audioOutputManager) : base(capi)
        {
            _voiceGroupManager = voiceGroupManager;
            _audioOutputManager = audioOutputManager;

            _voiceGroupManager.OnGroupUpdated += SetupDisplay;
            capi.Event.RegisterEventBusListener(OnHudUpdate, 0.5, "rpvoicechat:hudUpdate");

            SetupDisplay();
        }

        private void OnHudUpdate(string eventName, ref EnumHandling handling, IAttribute data)
        {
            capi.Event.EnqueueMainThreadTask(SetupDisplay, "rpvoicechat:GroupDisplay");
        }

        private void UpdateDisplay()
        {
            bool shouldDisplay = _voiceGroupManager.CurrentGroup is not null && ClientSettings.ShowHud;
            bool successful = shouldDisplay ? TryOpen() : TryClose();

            if (!successful) 
            {
                // Retry after a short delay if opening/closing failed
                capi.Event.EnqueueMainThreadTask(() => UpdateDisplay(), "rpvoicechat:GroupDisplay");
            }
        }

        private void SetupDisplay()
        {
            if (_voiceGroupManager.CurrentGroup is null)
            {
                UpdateDisplay();
                return;
            }    
            
            string groupName = _voiceGroupManager.CurrentGroup?.Name ?? "Unknown Group";
            string[] members = _voiceGroupManager.CurrentGroup?.Members ?? new string[0];

            // Calculate dynamic height based on member count
            float baseHeight = 60; // Height for title and padding
            float memberHeight = 20;
            float totalHeight = baseHeight + (members.Length * memberHeight);
            
            // Update dialog bounds with calculated height
            var bounds = new ElementBounds()
            {
                Alignment = EnumDialogArea.RightBottom,
                BothSizing = ElementSizing.Fixed,
                fixedHeight = totalHeight,
                fixedWidth = size,
                fixedPaddingX = 10,
                fixedPaddingY = 10
            };

            // Calculate bounds for group name and members list
            float memberStartY = 35;
            float memberFontSize = 12;
            float groupNameFontSize = 16;

            var composer = capi.Gui.CreateCompo("rpvcgroupdisplay", bounds)
                .AddShadedDialogBG(ElementBounds.Fill, true)
                .AddDialogTitleBar("Voice Group", OnTitleBarCloseClicked)
                .AddStaticText(
                    groupName,
                    CairoFont.WhiteDetailText().WithFontSize(groupNameFontSize).WithOrientation(EnumTextOrientation.Center),
                    ElementBounds.Fixed(0, 30, size, 24)
                );

            if (members.Length == 0)
            {
                composer.AddStaticText(
                    "No members",
                    CairoFont.WhiteDetailText().WithFontSize(memberFontSize).WithOrientation(EnumTextOrientation.Center),
                    ElementBounds.Fixed(0, memberStartY, size, memberHeight)
                );
            }
            else
            {
                for (int i = 0; i < members.Length; i++)
                {
                    string memberName = GetPlayerName(members[i]) ?? members[i];
                    bool isTalking = _audioOutputManager?.IsPlayerTalking(members[i]) ?? false;
                    
                    var memberBounds = ElementBounds.Fixed(10, memberStartY + i * memberHeight, size - 20, memberHeight);
                    
                    // Add talking indicator and member name
                    string displayText = isTalking ? "🔊 " + memberName : "   " + memberName;
                    var textColor = isTalking ? CairoFont.WhiteDetailText().WithColor(GuiStyle.ActiveButtonTextColor) 
                                              : CairoFont.WhiteDetailText();
                    
                    composer.AddStaticText(displayText, textColor.WithFontSize(memberFontSize), memberBounds);
                }
            }

            SingleComposer = composer.Compose();

            UpdateDisplay();
        }

        private string GetPlayerName(string playerUID)
        {
            var player = capi.World.PlayerByUid(playerUID);
            return player?.PlayerName ?? playerUID;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            SetupDisplay(); // Refresh display when opened
        }
    }
}
