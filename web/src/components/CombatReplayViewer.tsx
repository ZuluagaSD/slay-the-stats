'use client'

import { GameEvent } from '@/lib/types'
import { CreatureStatusBar } from '@/components/CreatureStatusBar'
import { EventLine } from '@/components/EventLine'

const HIDDEN_TYPES = new Set([
  'turn_start',
  'creature_snapshot',
  'combat_start',
  'combat_end',
  'deck_snapshot',
])

function extractCreatures(events: GameEvent[]): {
  playerCreatures: unknown[]
  enemyCreatures: unknown[]
} {
  let playerCreatures: unknown[] = []
  let enemyCreatures: unknown[] = []

  for (const ev of events) {
    const data = ev.data ?? {}
    if (ev.type === 'combat_start' || ev.type === 'turn_start' || ev.type === 'creature_snapshot') {
      if (Array.isArray(data.playerCreatures)) playerCreatures = data.playerCreatures
      if (Array.isArray(data.enemyCreatures)) enemyCreatures = data.enemyCreatures
      if (Array.isArray(data.players)) playerCreatures = data.players
      if (Array.isArray(data.enemies)) enemyCreatures = data.enemies
      if (Array.isArray(data.creatures)) {
        const pCreatures: unknown[] = []
        const eCreatures: unknown[] = []
        for (const c of data.creatures) {
          if (c && typeof c === 'object') {
            const rec = c as Record<string, unknown>
            const side = String(rec.side ?? '')
            const isPlayer = rec.isPlayer === true
            if (isPlayer || side === 'player' || side === 'Player') {
              pCreatures.push(c)
            } else {
              eCreatures.push(c)
            }
          }
        }
        if (pCreatures.length > 0) playerCreatures = pCreatures
        if (eCreatures.length > 0) enemyCreatures = eCreatures
      }
      if (playerCreatures.length > 0 || enemyCreatures.length > 0) break
    }
  }

  return { playerCreatures, enemyCreatures }
}

interface RoundGroup {
  round: number
  side: string
  events: GameEvent[]
}

/**
 * Consolidate consecutive card_drawn events (from hand draw) into a single
 * synthetic "Drew N cards" event to reduce visual noise.
 */
function consolidateEvents(events: GameEvent[]): GameEvent[] {
  const result: GameEvent[] = []
  let drawBatch: GameEvent[] = []

  function flushDraws() {
    if (drawBatch.length === 0) return
    if (drawBatch.length === 1) {
      result.push(drawBatch[0])
    } else {
      // Create a synthetic "drew N cards" event
      const firstDraw = drawBatch[0]
      result.push({
        ...firstDraw,
        type: 'cards_drawn_batch',
        data: {
          ...firstDraw.data,
          count: drawBatch.length,
          cardIds: drawBatch.map((e) => String(e.data?.cardId ?? '')),
        },
      })
    }
    drawBatch = []
  }

  for (const ev of events) {
    if (ev.type === 'card_drawn' && ev.data?.fromHandDraw) {
      drawBatch.push(ev)
    } else {
      flushDraws()
      result.push(ev)
    }
  }
  flushDraws()

  return result
}

function groupByRound(events: GameEvent[]): RoundGroup[] {
  const groups: RoundGroup[] = []
  let currentRound = 0
  let currentSide = ''
  let currentEvents: GameEvent[] = []

  for (const ev of events) {
    const data = ev.data ?? {}
    const evRound = Number(data.round ?? 0)
    const evSide = String(data.side ?? '')

    if (ev.type === 'turn_start' || (evRound > 0 && (evRound !== currentRound || evSide !== currentSide))) {
      if (currentEvents.length > 0) {
        groups.push({ round: currentRound, side: currentSide, events: currentEvents })
      }
      currentRound = evRound || currentRound + 1
      currentSide = evSide || currentSide
      currentEvents = []
    }

    if (!HIDDEN_TYPES.has(ev.type)) {
      currentEvents.push(ev)
    }
  }

  if (currentEvents.length > 0) {
    groups.push({ round: currentRound, side: currentSide, events: currentEvents })
  }

  // Consolidate card draws within each group
  return groups.map((g) => ({ ...g, events: consolidateEvents(g.events) }))
}

function formatSide(side: string): string {
  if (!side) return ''
  const lower = side.toLowerCase()
  if (lower === 'player' || lower === 'players') return 'Player Turn'
  if (lower === 'enemy' || lower === 'enemies') return 'Enemy Turn'
  return side
}

export function CombatReplayViewer({
  events,
  multiPlayer,
}: {
  events: GameEvent[]
  multiPlayer: boolean
}) {
  const { playerCreatures, enemyCreatures } = extractCreatures(events)
  const groups = groupByRound(events)

  if (events.length === 0) {
    return (
      <p className="text-[var(--text-muted)] text-sm py-4">
        Select a combat to view the replay.
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Creature status */}
      <CreatureStatusBar
        playerCreatures={playerCreatures}
        enemyCreatures={enemyCreatures}
      />

      {/* Event log */}
      <div className="overflow-y-auto max-h-[380px] pr-1">
        {groups.map((group, gi) => (
          <div key={gi}>
            {(group.round > 0 || group.side) && (
              <div className="round-divider">
                Round {group.round || gi + 1}
                {group.side ? ` \u00b7 ${formatSide(group.side)}` : ''}
              </div>
            )}
            {group.events.map((ev) => (
              <EventLine key={ev.id} event={ev} multiPlayer={multiPlayer} />
            ))}
          </div>
        ))}
      </div>
    </div>
  )
}
