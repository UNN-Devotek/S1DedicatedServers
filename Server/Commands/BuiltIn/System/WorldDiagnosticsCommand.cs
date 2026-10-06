using System;
using UnityEngine;
using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.ObjectScripts;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Levelling;
using ScheduleOne.NPCs;
using ScheduleOne.Persistence;
using ScheduleOne.Quests;
using ScheduleOne.GameTime;
using ScheduleOne.ObjectScripts;
#endif

namespace DedicatedServerMod.Server.Commands.BuiltIn.System
{
    /// <summary>Captures authoritative progression and NPC movement state without changing the world.</summary>
    internal sealed class WorldDiagnosticsCommand : BaseServerCommand
    {
        internal WorldDiagnosticsCommand(PlayerManager playerManager) : base(playerManager) { }

        /// <inheritdoc />
        public override string CommandWord => "worlddiagnostics";
        /// <inheritdoc />
        public override string Description => "Inspects rank, customer relationships, NPC pathing and quest IDs";
        /// <inheritdoc />
        public override string Usage => "worlddiagnostics [npc name | quests | clock]";
        /// <inheritdoc />
        public override string RequiredPermissionNode => PermissionNode.CreateConsoleCommandNode(CommandWord);

        /// <inheritdoc />
        public override void Execute(CommandContext context)
        {
            var load = Singleton<LoadManager>.Instance;
            if (load == null || load.IsLoading || !load.IsGameLoaded)
            {
                context.ReplyWarning("World diagnostics are available after the world finishes loading.");
                return;
            }

            var level = NetworkSingleton<LevelManager>.Instance;
            if (level != null)
            {
                context.Reply($"WORLD rank={level.Rank}, tier={level.Tier}, xp={level.XP}, totalXp={level.TotalXP}");
            }

            string filter = context.Arguments == null ? string.Empty : string.Join(" ", context.Arguments);
            if (string.Equals(filter, "clock", StringComparison.OrdinalIgnoreCase))
            {
                WriteClock(context);
                return;
            }
            if (string.Equals(filter, "quests", StringComparison.OrdinalIgnoreCase))
            {
                WriteQuests(context);
                return;
            }

            int inspected = 0;
            int unlocked = 0;
            int stranded = 0;
            var npcs = NPCManager.NPCRegistry;
            if (npcs != null)
            {
                for (int index = 0; index < npcs.Count; index++)
                {
                    var npc = npcs[index];
                    if (npc == null)
                    {
                        continue;
                    }

                    try
                    {
                        string name = npc.FullName ?? npc.name;
                        if (!string.IsNullOrWhiteSpace(filter)
                            && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }
                        inspected++;
                        var relation = npc.RelationData;
                        if (relation?.Unlocked == true) unlocked++;
#if GAME_BETA
                        var agent = npc.Movement?._agent;
                        bool active = npc.IsActive;
#else
                        var agent = npc.Movement?.Agent;
                        bool active = npc.gameObject.activeInHierarchy;
#endif
                        bool agentEnabled = agent != null && agent.isActiveAndEnabled;
                        bool onMesh = agentEnabled && agent.isOnNavMesh;
                        bool strandedNpc = active && npc.IsConscious && agentEnabled && !onMesh;
                        if (strandedNpc) stranded++;

                        // Default output stays short; a name filter includes healthy matching NPCs too.
                        if (!string.IsNullOrWhiteSpace(filter) || strandedNpc)
                        {
                            context.Reply(
                                $"NPC '{name}' region={npc.Region}, active={active}, conscious={npc.IsConscious}, " +
                                $"unlocked={relation?.Unlocked}, relationship={relation?.RelationDelta}, mutual={relation?.IsMutuallyKnown()}, " +
                                $"connections={relation?.Connections?.Count}, position={npc.transform.position}, " +
                                $"agentEnabled={agentEnabled}, onNavMesh={onMesh}, ragdolled={npc.Avatar?.Ragdolled}");
                        }
                    }
                    catch (Exception ex)
                    {
                        context.ReplyWarning($"NPC object '{npc.name}' could not be inspected: {ex.Message}");
                    }
                }
            }

            context.Reply($"WORLD inspectedNpcs={inspected}, unlockedNpcs={unlocked}, activeConsciousOffMesh={stranded}");
            context.Reply("Region progression uses the game's rank requirements; samples can award XP without immediately unlocking a region.");
        }

        private static void WriteClock(CommandContext context)
        {
            var clock = NetworkSingleton<TimeManager>.Instance;
            if (clock != null)
                context.Reply($"CLOCK time={clock.CurrentTime:D4}, day={clock.ElapsedDays}, speed={clock.TimeSpeedMultiplier}, unityScale={Time.timeScale}, endOfDay={clock.IsEndOfDay}, sleeping={DedicatedServerMod.Utils.SleepRuntime.IsSleepInProgress}");
#if GAME_BETA
            var sleep = NetworkSingleton<SleepController>.Instance;
            if (sleep != null)
                context.Reply($"SLEEP phase={sleep.CurrentPhase}, hostReady={sleep.IsHostReadyToProceed}, summaryInProgress={DailySummaryProgress()}, rankInProgress={RankProgress()}");
#endif
            foreach (var pot in UnityEngine.Object.FindObjectsOfType<Pot>())
            {
                if (pot != null && pot.Plant != null)
                    context.Reply($"PLANT pot='{pot.name}', progress={pot.Plant.NormalizedGrowthProgress:0.000000}, fullyGrown={pot.Plant.IsFullyGrown}");
            }
        }

#if GAME_BETA
        private static bool? DailySummaryProgress() =>
#if IL2CPP
            Il2CppScheduleOne.UI.DailySummary.Instance?.IsInProgress;
#else
            ScheduleOne.UI.DailySummary.Instance?.IsInProgress;
#endif
        private static bool? RankProgress() =>
#if IL2CPP
            UnityEngine.Object.FindObjectOfType<Il2CppScheduleOne.UI.RankUpCanvas>()?.IsInProgress;
#else
            UnityEngine.Object.FindObjectOfType<ScheduleOne.UI.RankUpCanvas>()?.IsInProgress;
#endif
#endif

        private static void WriteQuests(CommandContext context)
        {
            var quests = Quest.Quests;
            if (quests == null) return;
            for (int index = 0; index < quests.Count; index++)
            {
                var quest = quests[index];
                if (quest != null)
                {
                    context.Reply($"QUEST '{quest.GetQuestTitle()}' guid={quest.GUID}, state={quest.State}, tracked={quest.IsTracked}, entries={quest.Entries?.Count}");
                }
            }
        }
    }
}
