using HarmonyLib;
using System;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    class PatchManager : IDisposable
    {
        private Harmony harmony;

        public PatchManager(string harmonyId)
        {
            harmony = new Harmony(harmonyId);
        }

        public void Patch(ICoreAPI api)
        {
            if ((api.Side & EnumAppSide.Client) != 0) PatchClient();
            if ((api.Side & EnumAppSide.Server) != 0) PatchServer();
        }

        private void PatchClient()
        {
            EntityNameTagRendererRegistryPatch.Patch(harmony);
            GuiDialogCreateCharacterPatch.Patch(harmony);
            GuiElementSliderPatch.Patch(harmony);
            LoadedSoundNativePatch.Patch(harmony);
            SystemNetworkProcessPatch.Patch(harmony);
        }

        private void PatchServer()
        {
            NetworkAPIPatch.Patch(harmony);
            TcpNetServerPatch.Patch(harmony);
        }

        public void Unpatch()
        {
            harmony.UnpatchAll(harmony.Id);
        }

        public void Dispose()
        {
            Unpatch();
        }
    }
}
