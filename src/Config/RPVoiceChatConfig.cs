using System;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class RPVoiceChatConfig
    {
        private const string _version = "v1";
        public string Version;

        // --- Shared Config ---
        // These are meant to be set by anyone and
        // are used to change mod behavior
        public bool ManualPortForwarding = false;

        // --- Server Config ---
        // These are meant to be set by server admins and
        // are used to configure the server
        public int ServerPort = 52525;
        public string ServerIP = null;
        public bool AdditionalContent = true;

        // --- Client Settings ---
        // These are meant to be set by the client, but are
        // stored here for persistence across sessions
        [Obsolete("This setting was moved into ClientSettings and only kept here for backwards compatibility. It will soon be removed.")]
        public bool PushToTalkEnabled = false;
        [Obsolete("This setting was moved into ClientSettings and only kept here for backwards compatibility. It will soon be removed.")]
        public bool IsMuted = false;
        [Obsolete("This setting was moved into ClientSettings and only kept here for backwards compatibility. It will soon be removed.")]
        public int InputThreshold = 20;
        [Obsolete("This setting was moved into ClientSettings and only kept here for backwards compatibility. It will soon be removed.")]
        public string CurrentInputDevice;

        public RPVoiceChatConfig() { }

#pragma warning disable CS0618 // Type or member is obsolete
        public RPVoiceChatConfig(EnumAppSide side, RPVoiceChatConfig previousConfig)
        {
            Version = previousConfig.Version;
            ManualPortForwarding = previousConfig.ManualPortForwarding;

            ServerPort = previousConfig.ServerPort;
            ServerIP = previousConfig.ServerIP;
            AdditionalContent = previousConfig.AdditionalContent;

            PushToTalkEnabled = previousConfig.PushToTalkEnabled;
            IsMuted = previousConfig.IsMuted;
            InputThreshold = previousConfig.InputThreshold;
            CurrentInputDevice = previousConfig.CurrentInputDevice;

            if (Version != _version)
                UpdateConfigVersion(side);
        }

        public void UpdateConfigVersion(EnumAppSide side)
        {
            if (side == EnumAppSide.Server) return;
            ClientSettings.PushToTalkEnabled = PushToTalkEnabled;
            ClientSettings.IsMuted = IsMuted;
            ClientSettings.InputThreshold = (float)InputThreshold / 100;
            ClientSettings.CurrentInputDevice = CurrentInputDevice;
            ClientSettings.Save();
            Version = _version;
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }
}