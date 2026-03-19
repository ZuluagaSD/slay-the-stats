using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using SlayTheStats.Core;

namespace SlayTheStats.Collectors;

/// <summary>
/// Patches Hook.AfterPowerAmountChanged to record buffs and debuffs.
/// Signature: AfterPowerAmountChanged(CombatState, PowerModel, decimal amount, Creature? applier, CardModel?)
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPowerAmountChanged))]
public static class PowerCollector
{
    [HarmonyPostfix]
    public static void Postfix(
        CombatState combatState,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        try
        {
            if (power is null) return;

            int stacks = (int)amount;
            string powerId = power.Id.Entry;
            string powerName = power.Title.GetFormattedText();
            bool isDebuff = power.Type == PowerType.Debuff;

            if (isDebuff)
            {
                // Record debuff on the power's owner (target)
                var owner = power.GetType().GetProperty("Owner")?.GetValue(power) as Creature;
                int targetId = CollectorUtils.GetPlayerId(owner);
                int applierId = CollectorUtils.GetPlayerId(applier);
                StatManager.Instance.RecordDebuff(targetId, powerId, powerName, stacks, applierId);
            }
            else
            {
                // Record buff — only track on players
                var owner = power.GetType().GetProperty("Owner")?.GetValue(power) as Creature;
                int playerId = CollectorUtils.GetPlayerId(owner);
                if (playerId >= 0)
                {
                    StatManager.Instance.RecordBuff(playerId, powerId, powerName, stacks);
                }
            }
        }
        catch (Exception ex)
        {
            MainFile.LogError($"PowerCollector: {ex}");
        }
    }
}
