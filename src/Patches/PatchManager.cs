using HarmonyLib;
using System;

namespace rpvoicechat
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
            GuiDialogCreateCharacterPatch.Patch(harmony);
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
