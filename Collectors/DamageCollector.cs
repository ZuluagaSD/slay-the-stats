using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheStats.Core;

namespace SlayTheStats.Collectors;

/// <summary>
/// Patches Hook.AfterDamageGiven to record damage dealt by players.
/// Signature: AfterDamageGiven(PlayerChoiceContext, CombatState, Creature? dealer,
///            DamageResult results, ValueProp props, Creature target, CardModel? cardSource)
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
public static class DamageCollector
{
    [HarmonyPostfix]
    public static void Postfix(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        Creature? dealer,
        DamageResult results,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        try
        {
            if (dealer is null || !dealer.IsPlayer) return;

            int sourceId = CollectorUtils.GetPlayerId(dealer);
            if (sourceId < 0) return;

            int amount = results.TotalDamage;
            if (amount <= 0) return;

            int targetId = CollectorUtils.GetPlayerId(target);

            StatManager.Instance.RecordDamage(sourceId, targetId, amount);
        }
        catch (Exception ex)
        {
            MainFile.LogError($"DamageCollector: {ex}");
        }
    }
}
