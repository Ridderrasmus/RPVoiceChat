using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RPVoiceChat.Utils
{
    public class Logger
    {
        public ICoreAPI api;
        public static Logger server;
        public static Logger client;
        private object debug_file_lock = new object();
        private object main_file_lock = new object();

        public Logger(ICoreAPI api)
        {
            this.api = api;
            if (api is ICoreServerAPI) server = this;
            if (api is ICoreClientAPI) client = this;
        }

        public void Error(string message)
        {
            Error(message, new object[0]);
        }

        public void Error(string message, params object[] args)
        {
            lock (main_file_lock) api.Logger.Error($"[RPVoiceChat] {message}", args);
            lock (debug_file_lock) api.Logger.VerboseDebug($"[Error] [RPVoiceChat] {message}", args);
        }

        public void Debug(string message)
        {
            Debug(message, new object[0]);
        }

        public void Debug(string message, params object[] args)
        {
            lock (debug_file_lock) api.Logger.Debug($"[RPVoiceChat] {message}", args);
        }

        public void VerboseDebug(string message)
        {
            VerboseDebug(message, new object[0]);
        }

        public void VerboseDebug(string message, params object[] args)
        {
            lock (debug_file_lock) api.Logger.VerboseDebug($"[RPVoiceChat] {message}", args);
        }

        public void Warning(string message)
        {
            Warning(message, new object[0]);
        }

        public void Warning(string message, params object[] args)
        {
            lock (main_file_lock) api.Logger.Warning($"[RPVoiceChat] {message}", args);
            lock (debug_file_lock) api.Logger.VerboseDebug($"[Warning] [RPVoiceChat] {message}", args);
        }

        public void Notification(string message)
        {
            Notification(message, new object[0]);
        }

        public void Notification(string message, params object[] args)
        {
            lock (main_file_lock) api.Logger.Notification($"[RPVoiceChat] {message}", args);
            lock (debug_file_lock) api.Logger.VerboseDebug($"[Notification] [RPVoiceChat] {message}", args);
        }
    }
}
