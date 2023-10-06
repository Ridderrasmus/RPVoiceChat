using HarmonyLib;
using RPVoiceChat.Gui;
using RPVoiceChat.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Vintagestory.API.Client;

namespace RPVoiceChat
{
    /// <summary>
    /// Makes slider's tooltip render outside of clip bounds
    /// </summary>
    static class GuiElementSliderPatch
    {
        private static readonly CodeInstruction anchor = new CodeInstruction(
            OpCodes.Ldfld,
            AccessTools.Field(typeof(GuiElementSlider), "mouseDownOnSlider")
        );
        private static readonly CodeInstruction patchOneIndexPointer = new CodeInstruction(OpCodes.Brtrue_S);
        private static readonly CodeInstruction patchTwoIndexPointer = new CodeInstruction(OpCodes.Brfalse);
        private static List<CodeInstruction> firstPatch = new List<CodeInstruction>() {
            // Pass this.
            new CodeInstruction(OpCodes.Ldarg_0),
            // api
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(GuiElement), "api")),
            // and this
            new CodeInstruction(OpCodes.Ldarg_0),
            // Into DisableScissors
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GuiElementSliderPatch), nameof(DisableScissors)))
        };
        private static List<CodeInstruction> secondPatch = new List<CodeInstruction>() {
            // Pass this.
            new CodeInstruction(OpCodes.Ldarg_0),
            // api
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(GuiElement), "api")),
            // and this
            new CodeInstruction(OpCodes.Ldarg_0),
            // Into EnableScissors
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GuiElementSliderPatch), nameof(EnableScissors)))
        };

        public static void Patch(Harmony harmony)
        {
            var OriginalMethod = AccessTools.Method(typeof(GuiElementSlider), "RenderInteractiveElements");
            var TranspilerMethod = AccessTools.Method(typeof(GuiElementSliderPatch), nameof(RenderInteractiveElements_Transpiler));
            harmony.Patch(OriginalMethod, transpiler: new HarmonyMethod(TranspilerMethod));
        }

        public static IEnumerable<CodeInstruction> RenderInteractiveElements_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var firstPatchIndex = -1;
            var secondPatchIndex = -1;
            Label firstPatchLabel = default;
            Label secondPatchLabel = default;

            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                // First find transpiler anchor
                var codeInstr = codes[i];
                if (codeInstr.opcode != anchor.opcode) continue;
                if (codeInstr.operand != anchor.operand) continue;

                for (var j = i + 1; j < codes.Count; j++)
                {
                    // Then find pointers to where insert our patches
                    codeInstr = codes[j];
                    if (firstPatchLabel == default)
                    {
                        if (codeInstr.opcode == patchOneIndexPointer.opcode) firstPatchLabel = (Label)codeInstr.operand;
                        continue;
                    }
                    if (codeInstr.labels.Contains(firstPatchLabel))
                    {
                        firstPatchIndex = j;
                        codeInstr.labels.Remove(firstPatchLabel);
                    }

                    if (secondPatchLabel == default)
                    {
                        if (codeInstr.opcode == patchTwoIndexPointer.opcode) secondPatchLabel = (Label)codeInstr.operand;
                        continue;
                    }
                    if (codeInstr.labels.Contains(secondPatchLabel))
                    {
                        secondPatchIndex = j;
                        codeInstr.labels.Remove(secondPatchLabel);
                    }
                }
                break;
            }

            if (firstPatchIndex == -1 || secondPatchIndex == -1)
            {
                Logger.client.Error("Couldn't find transpiler anchor for GuiElementSlider patch");
                return instructions;
            }

            secondPatch[0].labels.Add(secondPatchLabel);
            firstPatch[0].labels.Add(firstPatchLabel);
            codes.InsertRange(secondPatchIndex, secondPatch);
            codes.InsertRange(firstPatchIndex, firstPatch);
            Logger.client.Notification("GuiElementSlider was successfully patched");

            return codes.AsEnumerable();
        }

        private static void DisableScissors(ICoreClientAPI api, GuiElementSlider slider)
        {
            bool ignoreClipBounds = api.Render.ScissorStack.Count > 0 && slider is NamedSlider;
            if (ignoreClipBounds) api.Render.GlScissorFlag(false);
        }

        private static void EnableScissors(ICoreClientAPI api, GuiElementSlider slider)
        {
            bool unignoreClipBounds = api.Render.ScissorStack.Count > 0 && slider is NamedSlider;
            if (unignoreClipBounds) api.Render.GlScissorFlag(true);
        }
    }
}
