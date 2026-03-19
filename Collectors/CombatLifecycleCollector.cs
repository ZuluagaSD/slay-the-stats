using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SlayTheStats.Core;

namespace SlayTheStats.Collectors;

/// <summary>
/// Patches combat lifecycle hooks:
/// - Hook.BeforeCombatStart(IRunState, CombatState?) → StatManager.StartCombat()
/// - Hook.AfterCombatEnd(IRunState, CombatState?, CombatRoom) → StatManager.EndCombat(false)
/// - Hook.AfterCombatVictory(IRunState, CombatState?, CombatRoom) → StatManager.EndCombat(true)
/// </summary>
public static class CombatLifecycleCollector
{
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
    public static class CombatStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(IRunState runState, CombatState? combatState)
        {
            try
            {
                // Ensure run is started
                if (StatManager.Instance.CurrentRun is null)
                    StatManager.Instance.StartRun();

                StatManager.Instance.StartCombat();

                // Register all players
                if (combatState is not null)
                {
                    foreach (var creature in combatState.Allies)
                    {
                        if (creature.IsPlayer)
                            CollectorUtils.EnsurePlayerRegistered(creature.Player);
                    }
                }
            }
            catch (Exception ex)
            {
                MainFile.LogError($"CombatStartPatch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
    public static class CombatVictoryPatch
    {
        [HarmonyPostfix]
        public static void Postfix(IRunState runState, CombatState? combatState, CombatRoom room)
        {
            try
            {
                StatManager.Instance.EndCombat(victory: true);
            }
            catch (Exception ex)
            {
                MainFile.LogError($"CombatVictoryPatch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
    public static class CombatEndPatch
    {
        [HarmonyPostfix]
        public static void Postfix(IRunState runState, CombatState? combatState, CombatRoom room)
        {
            try
            {
                // Only end if victory hasn't already ended it
                if (StatManager.Instance.CurrentCombat is not null)
                    StatManager.Instance.EndCombat(victory: false);
            }
            catch (Exception ex)
            {
                MainFile.LogError($"CombatEndPatch: {ex}");
            }
        }
    }
}
