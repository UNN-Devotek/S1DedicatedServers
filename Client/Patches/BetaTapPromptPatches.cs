#if GAME_BETA
using System;
using DedicatedServerMod.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.UI.Input;
using ReferenceList = Il2CppSystem.Collections.Generic.List<UnityEngine.InputSystem.InputActionReference>;
#else
using ScheduleOne;
using ScheduleOne.UI.Input;
using ReferenceList = System.Collections.Generic.List<UnityEngine.InputSystem.InputActionReference>;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Supplies the beta tap's mouse hint from its existing click action when the
    /// serialized fill descriptor only has a gamepad axis. Controls and source assets
    /// remain unchanged; the native renderer still resolves bindings and rebinding.
    /// </summary>
    [HarmonyPatch(typeof(InputPromptsManager), "GetPromptBindingsForCurrentControlScheme")]
    internal static class BetaTapPromptPatches
    {
        private static void Prefix(InputPromptsManager __instance,
            ref InputPromptsDescriptorData descriptor, out TemporaryPrompt __state)
        {
            __state = null;
            if (!DedicatedRuntimeContext.IsActive || GameInput.GetCurrentInputDeviceIsGamepad()
                || descriptor == null || descriptor.name != "Descriptor_FillContainer"
                || descriptor.Actions == null || descriptor.Actions.Count != 1)
            {
                return;
            }

            var fill = descriptor.Actions[0]?.action;
            if (fill == null || fill.name != "FillContainer" || fill.actionMap?.name != "Generic"
                || HasCurrentBinding(__instance, fill))
            {
                return;
            }

            var click = fill.actionMap.FindAction("PrimaryClick", false);
            if (click == null || !HasCurrentBinding(__instance, click))
            {
                return;
            }

            __state = new TemporaryPrompt();
            __state.Descriptor = UnityEngine.Object.Instantiate(descriptor);
            __state.Reference = InputActionReference.Create(click);
            __state.Descriptor.Actions = new ReferenceList();
            __state.Descriptor.Actions.Add(__state.Reference);
            descriptor = __state.Descriptor;
        }

        private static bool HasCurrentBinding(InputPromptsManager manager, InputAction action)
        {
            var bindings = action.bindings;
            for (int index = 0; index < bindings.Count; index++)
            {
                string path = bindings[index].effectivePath;
                if (!string.IsNullOrEmpty(path) && manager.HasCorrectControlScheme(path))
                {
                    return true;
                }
            }

            return false;
        }

        private static Exception Finalizer(TemporaryPrompt __state, Exception __exception)
        {
            if (__state != null)
            {
                UnityEngine.Object.Destroy(__state.Reference);
                UnityEngine.Object.Destroy(__state.Descriptor);
            }

            return __exception;
        }

        private sealed class TemporaryPrompt
        {
            internal InputPromptsDescriptorData Descriptor;
            internal InputActionReference Reference;
        }
    }
}
#endif
