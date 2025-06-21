using RPVoiceChat.VoiceGroups.Packets;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.VoiceGroups.Manager
{
    public class VoiceGroupManagerClient : VoiceGroupManagerCommon
    {
        public VoiceGroup CurrentGroup
        {
            get => _currentGroup;
        }
        private VoiceGroup _currentGroup = null;

        private ICoreClientAPI _capi;

        private IClientNetworkChannel _groupNetChannel;

        public event Action OnGroupUpdated;

        public VoiceGroupManagerClient(ICoreClientAPI capi) : base(capi)
        {
            _capi = capi;

            RegisterNetworkHandlers();

            OnGroupUpdated += SaveState;

            _capi.Event.LevelFinalize += LoadedWorld;
        }

        private void LoadedWorld()
        {
            // Load the current group state when the world is loaded
            LoadState();
        }

        private void RegisterNetworkHandlers()
        {
            _groupNetChannel = _capi.Network.GetChannel(_rpvcGroupNetworkChannelName);

            if (_groupNetChannel != null)
            {
                _groupNetChannel.SetMessageHandler<VoiceGroup>(HandleVoiceGroupUpdate);
            }
        }

        private void HandleVoiceGroupUpdate(VoiceGroup group)
        {
            if (group.Disbanded)
                _currentGroup = null;
            else
                _currentGroup = group;

            OnGroupUpdated?.Invoke();
        }

        private void LoadState()
        {
            // Load the current group state from persistent storage if available
            string groupName = _capi.World.Player.Entity.Attributes.GetAsString("rpvc-current-voice-group", string.Empty);

            if (!string.IsNullOrEmpty(groupName))
            {
                // Fetch the group information from the server
                _groupNetChannel.SendPacket(new VoiceGroupRequest { GroupName = groupName });
            }
            else
            {
                _currentGroup = null;
            }
        }

        private void SaveState()
        {
            // Save current client group state (Simplest for now will be save group name then on load full fetch from server)
            // TODO: This does not seem to work yet, need to investigate further
            _capi.World.Player.Entity.Attributes.SetString("rpvc-current-voice-group", _currentGroup?.Name ?? string.Empty);

        }
    }

}
