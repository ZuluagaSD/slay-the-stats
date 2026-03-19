using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheStats.Core;

namespace SlayTheStats.Collectors;

/// <summary>
/// Patches Hook.AfterBlockGained to record block gained by players.
/// Signature: AfterBlockGained(CombatState, Creature, decimal amount, ValueProp, CardModel?)
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockGained))]
public static class BlockCollector
{
    [HarmonyPostfix]
    public static void Postfix(
        CombatState combatState,
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        try
        {
            if (!creature.IsPlayer) return;

            int playerId = CollectorUtils.GetPlayerId(creature);
            if (playerId < 0) return;

            int blockAmount = (int)amount;
            if (blockAmount <= 0) return;

            StatManager.Instance.RecordBlock(playerId, blockAmount);
        }
        catch (Exception ex)
        {
            MainFile.LogError($"BlockCollector: {ex}");
        }
    }
}
