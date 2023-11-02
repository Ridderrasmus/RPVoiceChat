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
        public bool PushToTalkEnabled = false;
        public bool IsLoopbackEnabled = false;
        public bool IsHUDShown = true;
        public bool IsMuted = false;
        public int InputThreshold = 20;
        public string CurrentInputDevice;

        public RPVoiceChatConfig() { }

        public RPVoiceChatConfig(EnumAppSide side, RPVoiceChatConfig previousConfig)
        {
            Version = previousConfig.Version;
            ManualPortForwarding = previousConfig.ManualPortForwarding;

            ServerPort = previousConfig.ServerPort;
            ServerIP = previousConfig.ServerIP;
            AdditionalContent = previousConfig.AdditionalContent;

            PushToTalkEnabled = previousConfig.PushToTalkEnabled;
            IsLoopbackEnabled = previousConfig.IsLoopbackEnabled;
            IsHUDShown = previousConfig.IsHUDShown;
            IsMuted = previousConfig.IsMuted;
            InputThreshold = previousConfig.InputThreshold;
            CurrentInputDevice = previousConfig.CurrentInputDevice;

            if (Version != _version)
                UpdateConfigVersion(side);
        }

        public void UpdateConfigVersion(EnumAppSide side)
        {
            Version = _version;
        }
    }
}