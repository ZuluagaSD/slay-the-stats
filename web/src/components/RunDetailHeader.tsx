import { Run, RunPlayer, getCharacterConfig, formatDurationLong } from '@/lib/types'
import { StatusBadge } from '@/components/StatusBadge'

export function RunDetailHeader({
  run,
  players,
  multiPlayer,
}: {
  run: Run
  players: RunPlayer[]
  multiPlayer: boolean
}) {
  const primaryChar = players[0]?.character ?? 'Unknown'
  const config = getCharacterConfig(primaryChar)
  const charNames = players.map((p) => getCharacterConfig(p.character).name).join(' & ')

  const duration = formatDurationLong(run.run_time_seconds)

  return (
    <div className="run-header">
      <div className="flex items-center justify-between flex-wrap gap-4">
        {/* Left side: character name + badges */}
        <div className="flex items-center gap-4 flex-wrap">
          <h1 className="font-[family-name:var(--font-display)] text-2xl lg:text-3xl font-bold gold-gradient-text uppercase tracking-wide">
            {charNames}
          </h1>
          <span className="badge badge-ascension">Ascension {run.ascension}</span>
          <StatusBadge win={run.win} abandoned={run.abandoned} />
        </div>

        {/* Right side: seed, duration, floor */}
        <div className="flex items-center gap-6 text-sm">
          {run.seed && run.seed !== '?' && (
            <span className="text-[var(--text-muted)]">
              Seed ID: <span className="text-[var(--text-primary)]">#{run.seed}</span>
            </span>
          )}
          <div className="flex flex-col items-center">
            <span className="text-[var(--text-primary)] font-semibold text-base">{duration}</span>
            <span className="text-[var(--text-subtle)] text-xs">Run Duration</span>
          </div>
          <div className="flex flex-col items-center">
            <span className="text-[var(--text-primary)] font-semibold text-base">Floor {run.total_floors}</span>
          </div>
        </div>
      </div>
    </div>
  )
}
