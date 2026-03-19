'use client'

import { Combat, formatDuration } from '@/lib/types'
import { StatusBadge } from '@/components/StatusBadge'

export function CombatTimeline({
  combats,
  selectedId,
  onSelect,
}: {
  combats: Combat[]
  selectedId: string | null
  onSelect: (id: string) => void
}) {
  if (combats.length === 0) {
    return (
      <p className="text-[var(--text-muted)] text-sm py-4">No combats recorded.</p>
    )
  }

  return (
    <div className="timeline-track flex flex-col overflow-y-auto max-h-[420px]">
      {combats.map((combat, i) => {
        const isSelected = combat.id === selectedId
        const isVictory = combat.victory === true
        const isDefeat = combat.victory === false

        const duration =
          combat.started_at && combat.ended_at
            ? Math.round(
                (new Date(combat.ended_at).getTime() -
                  new Date(combat.started_at).getTime()) /
                  1000
              )
            : null

        const dotClass = [
          'timeline-dot',
          isSelected && 'timeline-dot-active',
          !isSelected && isVictory && 'timeline-dot-victory',
          !isSelected && isDefeat && 'timeline-dot-defeat',
        ]
          .filter(Boolean)
          .join(' ')

        return (
          <button
            key={combat.id}
            type="button"
            onClick={() => onSelect(combat.id)}
            className={`timeline-item ${isSelected ? 'timeline-item-selected' : ''}`}
          >
            <div className={dotClass} />

            <div className="flex items-center justify-between gap-2 w-full">
              <div className="flex flex-col gap-0.5 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-sm font-bold text-[var(--text-primary)] truncate">
                    {combat.encounter_id ?? `Combat ${i + 1}`}
                  </span>
                </div>
                <div className="flex items-center gap-2 text-xs text-[var(--text-muted)]">
                  {combat.victory !== null && (
                    <StatusBadge win={combat.victory} />
                  )}
                  {combat.final_round != null && (
                    <span>Round: {combat.final_round}</span>
                  )}
                  <span className="text-[var(--text-subtle)]">Duration</span>
                </div>
              </div>

              {/* Duration number on the right */}
              <span className="text-lg font-bold text-[var(--text-primary)] flex-shrink-0 tabular-nums">
                {duration != null ? duration : '—'}
              </span>
            </div>
          </button>
        )
      })}
    </div>
  )
}
