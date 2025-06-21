using RPVoiceChat.Audio;
using RPVoiceChat.Gui.CustomElements;
using RPVoiceChat.VoiceGroups.Manager;
using System;
using System.Collections.Generic;
using System.Drawing;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RPVoiceChat.Gui
{
    internal class GroupDisplay : HudElement
    {
        private VoiceGroupManagerClient _voiceGroupManager;
        private AudioOutputManager _audioOutputManager;

        private const float size = 128;
        private ElementBounds dialogBounds = new ElementBounds()
        {
            Alignment = EnumDialogArea.RightBottom,
            BothSizing = ElementSizing.Fixed,
            fixedHeight = size,
            fixedWidth = size,
            fixedPaddingX = 10,
            fixedPaddingY = 10 * 2 + size
        };

        // TODO: Need to make this work. Putting this off for now as it should at least update on group changes happening.

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

            if (!successful) UpdateDisplay();

        }

        // Replace the SetupDisplay method with the following implementation

        private void SetupDisplay()
        {
            if (_voiceGroupManager.CurrentGroup is null)
            {
                UpdateDisplay();
                return;
            }    
            
            string groupName = _voiceGroupManager.CurrentGroup?.Name ?? "";
            string[] members = _voiceGroupManager.CurrentGroup?.Members ?? new string[] { "Player1", "Player2" };

            if (string.IsNullOrWhiteSpace(groupName))
            {
                groupName = "No Group";
            }

            // Calculate bounds for group name and members list
            float memberStartY = 30;
            float memberHeight = 20;
            float memberFontSize = 14;
            float groupNameFontSize = 16;

            var composer = capi.Gui.CreateCompo("rpvcgroupdisplay", dialogBounds)
                .AddStaticText(
                    groupName,
                    CairoFont.WhiteDetailText().WithFontSize(groupNameFontSize).WithOrientation(EnumTextOrientation.Center),
                    ElementBounds.Fixed(0, 0, size, 24)
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
                    composer.AddInteractiveElement(new GroupMemberElement(capi, ElementBounds.Fixed(10, memberStartY + i * memberHeight, size - 20, memberHeight), members[i], _audioOutputManager));
                }
            }


            SingleComposer = composer.Compose();

            UpdateDisplay();
        }
    }


}
