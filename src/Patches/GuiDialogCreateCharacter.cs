using HarmonyLib;
using System;
using Vintagestory.GameContent;

namespace RPVoiceChat
{
    static class GuiDialogCreateCharacterPatch
    {
        public static event Action OnCharacterSelection;

        public static void Patch(Harmony harmony) {
            var OriginalMethod = typeof(GuiDialogCreateCharacter).GetMethod(nameof(GuiDialogCreateCharacter.OnGuiClosed));
            var PostfixMethod = typeof(GuiDialogCreateCharacterPatch).GetMethod(nameof(GuiDialogCreateCharacterPatch.OnGuiClosed));
            harmony.Patch(OriginalMethod, postfix: new HarmonyMethod(PostfixMethod));
        }

        public static void OnGuiClosed()
        {
            OnCharacterSelection.Invoke();
        }
    }
}
