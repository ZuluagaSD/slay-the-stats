'use client'

import { useState, useMemo } from 'react'
import { Combat, GameEvent, DeckSnapshot } from '@/lib/types'
import { CombatTimeline } from '@/components/CombatTimeline'
import { CombatReplayViewer } from '@/components/CombatReplayViewer'
import { DeckEvolution } from '@/components/DeckEvolution'
import { DamageStatsTable } from '@/components/DamageStatsTable'

/**
 * Extract events belonging to a specific combat.
 * Strategy:
 *   1. Find the combat_start event whose data.combatId matches the short combat id
 *   2. Collect all events from that combat_start until the next combat_end (inclusive)
 *   3. If no combatId-based match, fallback to matching by encounterId / seq range
 */
function getEventsForCombat(allEvents: GameEvent[], combatId: string): GameEvent[] {
  // The DB combat id is like "runId_shortId", event data uses just the shortId
  const shortId = combatId.includes('_') ? combatId.split('_').slice(1).join('_') : combatId

  // Find the combat_start event index
  let startIdx = -1
  for (let i = 0; i < allEvents.length; i++) {
    const ev = allEvents[i]
    if (ev.type === 'combat_start') {
      const evCombatId = String(ev.data?.combatId ?? '')
      if (evCombatId === shortId || evCombatId === combatId) {
        startIdx = i
        break
      }
    }
  }

  if (startIdx === -1) return []

  // Collect events from startIdx to the next combat_end (inclusive)
  const result: GameEvent[] = []
  for (let i = startIdx; i < allEvents.length; i++) {
    result.push(allEvents[i])
    if (allEvents[i].type === 'combat_end' && i > startIdx) break
  }

  return result
}

export function RunDetailBody({
  combats,
  events,
  deckSnapshots,
  multiPlayer,
}: {
  combats: Combat[]
  events: GameEvent[]
  deckSnapshots: DeckSnapshot[]
  multiPlayer: boolean
}) {
  const [selectedCombatId, setSelectedCombatId] = useState<string | null>(
    combats.length > 0 ? combats[0].id : null
  )

  const combatEvents = useMemo(
    () => (selectedCombatId ? getEventsForCombat(events, selectedCombatId) : []),
    [events, selectedCombatId]
  )

  return (
    <div className="flex flex-col gap-4">
      {/* Row 1: Timeline + Replay */}
      <div className="flex gap-4 flex-col lg:flex-row">
        {/* Combat Timeline */}
        <div className="glass-card p-4 w-full lg:w-[320px] flex-shrink-0">
          <h2 className="font-[family-name:var(--font-display)] text-base font-semibold text-[var(--gold-light)] mb-3">
            Combat Timeline
          </h2>
          <CombatTimeline
            combats={combats}
            selectedId={selectedCombatId}
            onSelect={setSelectedCombatId}
          />
        </div>

        {/* Combat Replay Viewer */}
        <div className="glass-card p-4 flex-1 min-w-0">
          <h2 className="font-[family-name:var(--font-display)] text-base font-semibold text-[var(--gold-light)] mb-3">
            Combat Replay Viewer
          </h2>
          <CombatReplayViewer events={combatEvents} multiPlayer={multiPlayer} />
        </div>
      </div>

      {/* Row 2: Deck Evolution + Damage Stats */}
      <div className="flex gap-4 flex-col lg:flex-row">
        {/* Deck Evolution */}
        <div className="glass-card p-4 flex-1 min-w-0">
          <h2 className="font-[family-name:var(--font-display)] text-base font-semibold text-[var(--gold-light)] mb-3">
            Deck Evolution
          </h2>
          <DeckEvolution snapshots={deckSnapshots} />
        </div>

        {/* Damage Stats */}
        <div className="glass-card p-4 w-full lg:w-[380px] flex-shrink-0">
          <h2 className="font-[family-name:var(--font-display)] text-base font-semibold text-[var(--gold-light)] mb-3">
            Damage Stats
          </h2>
          <DamageStatsTable events={combatEvents} />
        </div>
      </div>
    </div>
  )
}
