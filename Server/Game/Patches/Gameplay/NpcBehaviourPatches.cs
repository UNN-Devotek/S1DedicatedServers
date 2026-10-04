using System;
using HarmonyLib;
using DedicatedServerMod.Utils;
using UnityEngine;
#if IL2CPP
using BehaviourType = Il2CppScheduleOne.NPCs.Behaviour.Behaviour;
using BehaviourListType = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.NPCs.Behaviour.Behaviour>;
using NpcBehaviourType = Il2CppScheduleOne.NPCs.Behaviour.NPCBehaviour;
#else
using BehaviourType = ScheduleOne.NPCs.Behaviour.Behaviour;
using BehaviourListType = System.Collections.Generic.List<ScheduleOne.NPCs.Behaviour.Behaviour>;
using NpcBehaviourType = ScheduleOne.NPCs.Behaviour.NPCBehaviour;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Removes per-frame LINQ allocation and reflection overhead from NPC behavior selection on dedicated headless servers.
    /// Uses the Krafs-publicized <c>enabledBehaviours</c> field directly so the hot path stays out of <see cref="Utils.SafeReflection"/>.
    /// Validates police targets before transitions and releases police behaviours whose transition fails with a null reference.
    /// Cleanup runs for each affected behaviour; only repeated diagnostics share a cooldown.
    /// </summary>
    [HarmonyPatch(typeof(NpcBehaviourType), "Update")]
    internal static class NpcBehaviourUpdatePatches
    {
        private const float RECOVERY_WARNING_COOLDOWN_SECONDS = 3f;

        private static float _lastRecoveryWarningTime = -RECOVERY_WARNING_COOLDOWN_SECONDS;
        private static int _recoveryExceptionCount;

        private static bool Prefix(NpcBehaviourType __instance)
        {
            if (__instance == null || __instance.DEBUG_MODE)
            {
                return true;
            }

            BehaviourType processingBehaviour = null;
            string stage = "update";
            try
            {
                if (__instance.IsServerInitialized)
                {
                    BehaviourListType enabledBehaviours = __instance.enabledBehaviours;
                    BehaviourType enabledBehaviour = enabledBehaviours != null && enabledBehaviours.Count > 0
                        ? enabledBehaviours[0]
                        : null;

                    // Do not pause the current behaviour for a pursuit which cannot safely start.
                    if (DedicatedPolicePursuitAuthority.TryClearInvalidPoliceBehaviour(enabledBehaviour))
                    {
                        return false;
                    }

                    if (enabledBehaviour != __instance.activeBehaviour)
                    {
                        processingBehaviour = __instance.activeBehaviour;
                        stage = "pause";
                        if (processingBehaviour != null)
                        {
                            processingBehaviour.Pause_Server();
                        }

                        processingBehaviour = enabledBehaviour;
                        if (enabledBehaviour != null)
                        {
                            if (enabledBehaviour.Started)
                            {
                                stage = "resume";
                                enabledBehaviour.Resume_Server();
                            }
                            else
                            {
                                stage = "activate";
                                enabledBehaviour.Activate_Server(null);
                            }
                        }
                    }
                }

                stage = "update";
                processingBehaviour = __instance.activeBehaviour;
                if (processingBehaviour != null && processingBehaviour.Active)
                {
                    if (!DedicatedPolicePursuitAuthority.TryClearInvalidPoliceBehaviour(processingBehaviour))
                    {
                        processingBehaviour.BehaviourUpdate();
                    }
                }
            }
            catch (Exception ex)
            {
                bool stalePoliceTarget = DedicatedPolicePursuitAuthority.HasInvalidPoliceTarget(processingBehaviour);
                bool failedPoliceTransition = stage != "update"
                    && IsNullReferenceFailure(ex)
                    && DedicatedPolicePursuitAuthority.IsPoliceBehaviour(processingBehaviour);
                if (!__instance.IsServerInitialized || (!stalePoliceTarget && !failedPoliceTransition))
                {
                    throw;
                }

                // Always release this behaviour, even while another officer's diagnostic is throttled.
                bool disabled = DedicatedPolicePursuitAuthority.TryDisablePoliceBehaviour(processingBehaviour);
                _recoveryExceptionCount++;
                float now = Time.realtimeSinceStartup;
                if (now - _lastRecoveryWarningTime >= RECOVERY_WARNING_COOLDOWN_SECONDS)
                {
                    _lastRecoveryWarningTime = now;
                    int exceptionCount = _recoveryExceptionCount;
                    _recoveryExceptionCount = 0;
                    DebugLog.Warning(
                        $"Police behaviour recovery ({exceptionCount} exception(s)): " +
                        $"NPC='{processingBehaviour?.Npc?.FullName ?? "unknown"}', " +
                        $"behaviour={processingBehaviour?.GetType().Name}, stage={stage}, " +
                        $"invalidTarget={stalePoliceTarget}, disabled={disabled}.", ex);
                }
            }

            return false;
        }

        private static bool IsNullReferenceFailure(Exception exception)
        {
            if (exception is NullReferenceException)
            {
                return true;
            }

#if IL2CPP
            // Native game exceptions are wrapped by Il2CppInterop rather than mapped to CLR types.
            // Match the formatted native type, not arbitrary messages containing the word "null".
            return exception is Il2CppInterop.Runtime.Il2CppException
                && exception.Message?.StartsWith("System.NullReferenceException:", StringComparison.Ordinal) == true;
#else
            return false;
#endif
        }
    }
}
