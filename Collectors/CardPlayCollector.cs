using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using SlayTheStats.Core;

namespace SlayTheStats.Collectors;

/// <summary>
/// Patches Hook.AfterCardPlayed to record cards played by each player.
/// Signature: AfterCardPlayed(CombatState, PlayerChoiceContext, CardPlay)
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
public static class CardPlayCollector
{
    [HarmonyPostfix]
    public static void Postfix(
        CombatState combatState,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        try
        {
            var card = cardPlay.Card;
            if (card is null) return;

            var owner = card.Owner;
            if (owner is null) return;

            int playerId = CollectorUtils.GetPlayerId(owner);
            if (playerId < 0) return;

            string cardId = card.Id.Entry;
            string cardName = card.Title;
            int energyCost = card.CurrentStarCost;
            string cardType = card.Type.ToString();

            StatManager.Instance.RecordCardPlay(playerId, cardId, cardName, energyCost, cardType);
        }
        catch (Exception ex)
        {
            MainFile.LogError($"CardPlayCollector: {ex}");
        }
    }
}
