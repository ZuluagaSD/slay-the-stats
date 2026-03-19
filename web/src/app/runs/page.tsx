import { supabase } from '@/lib/supabase'
import { Run, RunPlayer, Combat, getCharacterConfig, formatTimeAgo, formatDuration } from '@/lib/types'
import { StatusBadge } from '@/components/StatusBadge'
import { Hash, Map, Swords, Clock } from 'lucide-react'
import Link from 'next/link'

export const dynamic = 'force-dynamic'

export default async function RunsPage() {
  const [runsRes, playersRes, combatsRes] = await Promise.all([
    supabase.from('runs').select('*').order('started_at', { ascending: false }),
    supabase.from('run_players').select('*'),
    supabase.from('combats').select('run_id, victory'),
  ])

  const runs = (runsRes.data ?? []) as Run[]
  const players = (playersRes.data ?? []) as RunPlayer[]
  const combats = (combatsRes.data ?? []) as { run_id: string; victory: boolean | null }[]

  const playersByRun: Record<string, RunPlayer[]> = {}
  for (const p of players) {
    if (!playersByRun[p.run_id]) playersByRun[p.run_id] = []
    playersByRun[p.run_id].push(p)
  }

  const combatsByRun: Record<string, number> = {}
  for (const c of combats) {
    combatsByRun[c.run_id] = (combatsByRun[c.run_id] ?? 0) + 1
  }

  return (
    <div>
      <h1 className="font-[family-name:var(--font-display)] text-3xl font-bold text-[var(--gold-light)] mb-6">
        Run History
      </h1>

      {runs.length === 0 ? (
        <div className="glass-card p-10 text-center">
          <p className="text-[var(--text-muted)]">No runs recorded yet. Start playing!</p>
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          {runs.map((run) => {
            const runPlayers = playersByRun[run.id] ?? []
            const primaryChar = runPlayers[0]?.character ?? 'Unknown'
            const config = getCharacterConfig(primaryChar)
            const charNames = runPlayers.map(p => getCharacterConfig(p.character).name).join(' & ')
            const combatCount = combatsByRun[run.id] ?? 0

            return (
              <Link key={run.id} href={`/runs/${run.id}`}>
                <div
                  className="glass-card px-5 py-4 cursor-pointer transition-colors hover:bg-[var(--bg-card-hover)] flex items-center gap-4"
                  style={{ borderLeft: `3px solid ${config.color}` }}
                >
                  {/* Character + Ascension */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-3 flex-wrap">
                      <span className="font-semibold text-[var(--text-primary)]">{charNames}</span>
                      <span className="text-[var(--gold)] font-bold text-sm">A{run.ascension}</span>
                      <StatusBadge win={run.win} abandoned={run.abandoned} />
                    </div>
                    <div className="flex items-center gap-4 mt-1 text-xs text-[var(--text-muted)]">
                      {run.seed && run.seed !== '?' && (
                        <span className="flex items-center gap-1"><Hash className="w-3 h-3" />{run.seed}</span>
                      )}
                      <span className="flex items-center gap-1"><Map className="w-3 h-3" />{run.total_floors} floors</span>
                      <span className="flex items-center gap-1"><Swords className="w-3 h-3" />{combatCount} combats</span>
                      <span className="flex items-center gap-1"><Clock className="w-3 h-3" />{formatDuration(run.run_time_seconds)}</span>
                    </div>
                  </div>

                  {/* Time ago */}
                  <span className="text-xs text-[var(--text-subtle)] flex-shrink-0">
                    {formatTimeAgo(run.started_at)}
                  </span>
                </div>
              </Link>
            )
          })}
        </div>
      )}
    </div>
  )
}
