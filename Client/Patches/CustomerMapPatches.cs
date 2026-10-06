using DedicatedServerMod.Utils;
using HarmonyLib;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.UI.Phone.Map;
#else
using FishNet;
using ScheduleOne.Economy;
using ScheduleOne.NPCs;
using ScheduleOne.UI.Phone.Map;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Recomputes potential-customer markers when the map opens. Initial relationship
    /// replication can precede the connection-unlock subscriptions used by the native UI.
    /// Uses the native visibility rule and does not change customer or region unlock state.
    /// </summary>
    [HarmonyPatch(typeof(MapApp), "SetOpen")]
    internal static class CustomerMapPatches
    {
        private static void Postfix(bool open)
        {
            if (!open || !DedicatedRuntimeContext.IsActive || InstanceFinder.IsServer)
            {
                return;
            }

            var npcs = NPCManager.NPCRegistry;
            if (npcs == null)
            {
                return;
            }

            for (int index = 0; index < npcs.Count; index++)
            {
                var npc = npcs[index];
                if (npc != null)
                {
                    var customer = npc.GetComponent<Customer>();
                    if (customer?.NPC != null && customer.NPC.RelationData != null)
                    {
                        customer.UpdatePotentialCustomerPoI();
                    }
                }
            }
        }
    }
}
