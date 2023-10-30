using HarmonyLib;
using System;
using Vintagestory.GameContent;

namespace RPVoiceChat
{
    static class GuiDialogCreateCharacterPatch
    {
        public static event Action OnCharacterSelection;

        public static void Patch(Harmony harmony)
        {
            var OriginalMethod = AccessTools.Method(typeof(GuiDialogCreateCharacter), nameof(GuiDialogCreateCharacter.OnGuiClosed));
            var PostfixMethod = AccessTools.Method(typeof(GuiDialogCreateCharacterPatch), nameof(OnGuiClosed));
            harmony.Patch(OriginalMethod, postfix: new HarmonyMethod(PostfixMethod));
        }

        public static void OnGuiClosed()
        {
            OnCharacterSelection?.Invoke();
        }
    }
}
