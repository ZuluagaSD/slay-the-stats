using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using SlayTheStats.Core;

namespace SlayTheStats.Collectors;

/// <summary>
/// Patches turn lifecycle hooks:
/// - Hook.AfterPlayerTurnStart(CombatState, PlayerChoiceContext, Player) → increment turn counter
/// - Hook.AfterTurnEnd(CombatState, CombatSide) → (informational)
/// </summary>
public static class TurnCollector
{
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
    public static class TurnStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(
            CombatState combatState,
            PlayerChoiceContext choiceContext,
            Player player)
        {
            try
            {
                StatManager.Instance.IncrementTurn();
            }
            catch (Exception ex)
            {
                MainFile.LogError($"TurnStartPatch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterTurnEnd))]
    public static class TurnEndPatch
    {
        [HarmonyPostfix]
        public static void Postfix(CombatState combatState, CombatSide side)
        {
            // Informational — turn counter already incremented on start
        }
    }
}
