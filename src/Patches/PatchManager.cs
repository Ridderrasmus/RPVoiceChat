using HarmonyLib;
using System;

namespace RPVoiceChat
{
    class PatchManager : IDisposable
    {
        private Harmony harmony;

        public PatchManager(string harmonyId)
        {
            harmony = new Harmony(harmonyId);
        }

        public void Patch()
        {
            EntityNameTagRendererRegistryPatch.Patch(harmony);
            GuiDialogCreateCharacterPatch.Patch(harmony);
            GuiElementSliderPatch.Patch(harmony);
            LoadedSoundNativePatch.Patch(harmony);
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
