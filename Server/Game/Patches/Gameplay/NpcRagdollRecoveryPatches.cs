using System;
using System.Runtime.CompilerServices;
using DedicatedServerMod.Utils;
using HarmonyLib;
using UnityEngine;
#if IL2CPP
using NpcMovementType = Il2CppScheduleOne.NPCs.NPCMovement;
using NpcType = Il2CppScheduleOne.NPCs.NPC;
#else
using NpcMovementType = ScheduleOne.NPCs.NPCMovement;
using NpcType = ScheduleOne.NPCs.NPC;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Restores conscious NPCs whose headless ragdoll physics never reaches the vanilla velocity threshold.
    /// </summary>
    /// <remarks>
    /// Vanilla recovery remains authoritative and gets the first opportunity to run. This postfix only invokes the
    /// existing networked deactivation path after the NPC has remained continuously eligible for recovery for a
    /// conservative fallback interval and with a finite root, hip, and skeleton pose. Invalid poses are diagnosed
    /// instead of being forced through the stand-up alignment path. Knocked-out, dead, unconscious, paused,
    /// and seizure ragdolls are excluded. This guard applies to the fallback, not vanilla recovery.
    /// </remarks>
    [HarmonyPatch(typeof(NpcMovementType), "FixedUpdate")]
    internal static class NpcRagdollRecoveryPatches
    {
        private const float FALLBACK_RECOVERY_SECONDS = 8f;

        private static readonly ConditionalWeakTable<NpcMovementType, RecoveryState> RecoveryStates = new();
        private static readonly ConditionalWeakTable<NpcMovementType, EligibilityFailureState> EligibilityFailures = new();
        private static readonly ConditionalWeakTable<NpcMovementType, PoseFailureState> PoseFailures = new();

        private static void Postfix(NpcMovementType __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (!IsEligibleForFallback(__instance, out NpcType npc))
            {
                RecoveryStates.Remove(__instance);
                return;
            }

            if (!RecoveryStates.TryGetValue(__instance, out RecoveryState state))
            {
                state = new RecoveryState(Time.realtimeSinceStartup);
                RecoveryStates.Add(__instance, state);
                return;
            }

            float elapsedSeconds = Time.realtimeSinceStartup - state.EligibleSince;
            if (elapsedSeconds < FALLBACK_RECOVERY_SECONDS)
            {
                return;
            }

            RecoveryStates.Remove(__instance);
            try
            {
                if (!HasFiniteRecoveryPose(npc, out string invalidPose))
                {
                    WarnInvalidPoseOnce(__instance, npc, $"before fallback: {invalidPose}");
                    return;
                }

                PoseFailures.Remove(__instance);
                __instance.DeactivateRagdoll();
                // An observers RPC request is not proof that every client completed the transition.
                DebugLog.Info(
                    $"Requested fallback ragdoll recovery for conscious NPC '{npc.FullName}' after headless " +
                    $"ragdoll physics remained unsettled for {elapsedSeconds:F1}s.");
                if (!HasFiniteRecoveryPose(npc, out invalidPose))
                {
                    WarnInvalidPoseOnce(__instance, npc, $"after fallback request: {invalidPose}");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Failed to recover unsettled NPC ragdoll '{npc.FullName}': {ex.Message}");
            }
        }

        private static bool IsEligibleForFallback(NpcMovementType movement, out NpcType npc)
        {
            npc = null;
            if (movement == null || !movement.IsServerInitialized
#if !GAME_BETA
                || movement.IsPaused
#endif
            )
            {
                return false;
            }

#if GAME_BETA
            npc = movement._npc;
#else
            npc = movement.npc;
#endif
            if (npc == null ||
                npc.Avatar == null ||
                npc.Health == null ||
                !npc.Avatar.Ragdolled ||
                !npc.IsConscious ||
                npc.Health.IsDead ||
                npc.Health.IsKnockedOut)
            {
                return false;
            }

            try
            {
                bool canRecover = movement.CanRecoverFromRagdoll();
                EligibilityFailures.Remove(movement);
                return canRecover;
            }
            catch (Exception ex)
            {
                if (!EligibilityFailures.TryGetValue(movement, out _))
                {
                    EligibilityFailures.Add(movement, new EligibilityFailureState());
                    DebugLog.Warning(
                        $"Could not evaluate ragdoll recovery eligibility for '{npc.FullName}': {ex.Message}");
                }

                return false;
            }
        }

        private static bool HasFiniteRecoveryPose(NpcType npc, out string invalidPose)
        {
            invalidPose = null;
            if (!HasFiniteTransform(npc.transform) || !HasFiniteTransform(npc.Avatar.transform))
            {
                invalidPose = "root transform";
                return false;
            }

            if (!HasFiniteTransform(npc.Avatar.HipBone))
            {
                invalidPose = "hip transform";
                return false;
            }

            var animation = npc.Avatar.Animation;
#if GAME_BETA
            var bones = animation != null ? animation._bones : null;
#else
            var bones = animation != null ? animation.Bones : null;
#endif
            if (bones == null || bones.Length == 0)
            {
                invalidPose = "missing animation bones";
                return false;
            }

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (!HasFiniteTransform(bone))
                {
                    invalidPose = $"bone[{i}] '{(bone != null ? bone.name : "missing")}'";
                    return false;
                }
            }

            return true;
        }

        private static bool HasFiniteTransform(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            Vector3 scale = transform.lossyScale;
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)
                || !IsFinite(rotation.x) || !IsFinite(rotation.y) || !IsFinite(rotation.z) || !IsFinite(rotation.w)
                || !IsFinite(scale.x) || !IsFinite(scale.y) || !IsFinite(scale.z))
            {
                return false;
            }

            // A finite world position alone can hide a corrupt parent scale/collider matrix.
            Matrix4x4 matrix = transform.localToWorldMatrix;
            // Use named fields: generated IL2CPP matrix indexers are not consumable by C#.
            return IsFinite(matrix.m00) && IsFinite(matrix.m01) && IsFinite(matrix.m02) && IsFinite(matrix.m03)
                && IsFinite(matrix.m10) && IsFinite(matrix.m11) && IsFinite(matrix.m12) && IsFinite(matrix.m13)
                && IsFinite(matrix.m20) && IsFinite(matrix.m21) && IsFinite(matrix.m22) && IsFinite(matrix.m23)
                && IsFinite(matrix.m30) && IsFinite(matrix.m31) && IsFinite(matrix.m32) && IsFinite(matrix.m33);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void WarnInvalidPoseOnce(NpcMovementType movement, NpcType npc, string detail)
        {
            if (PoseFailures.TryGetValue(movement, out _))
            {
                return;
            }

            PoseFailures.Add(movement, new PoseFailureState());
            DebugLog.Warning($"NPC '{npc.FullName}' has an invalid pose {detail}; fallback recovery cannot be confirmed safe.");
        }

        private sealed class PoseFailureState
        {
        }

        private sealed class EligibilityFailureState
        {
        }

        private sealed class RecoveryState
        {
            internal RecoveryState(float eligibleSince)
            {
                EligibleSince = eligibleSince;
            }

            internal float EligibleSince { get; }
        }
    }
}
