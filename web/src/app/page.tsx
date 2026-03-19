import { supabase } from '@/lib/supabase'
import { Run, RunPlayer, getCharacterConfig, formatTimeAgo } from '@/lib/types'
import { StatusBadge } from '@/components/StatusBadge'
import { Flame, Trophy, Map, Swords, Clock } from 'lucide-react'
import Link from 'next/link'

export const dynamic = 'force-dynamic'

export default async function DashboardPage() {
  const [runsRes, playersRes, combatsRes] = await Promise.all([
    supabase.from('runs').select('*').order('started_at', { ascending: false }),
    supabase.from('run_players').select('*'),
    supabase.from('combats').select('id'),
  ])

  const runs = (runsRes.data ?? []) as Run[]
  const players = (playersRes.data ?? []) as RunPlayer[]
  const totalCombats = combatsRes.data?.length ?? 0

  const playersByRun: Record<string, RunPlayer[]> = {}
  for (const p of players) {
    if (!playersByRun[p.run_id]) playersByRun[p.run_id] = []
    playersByRun[p.run_id].push(p)
  }

  const totalRuns = runs.length
  const wins = runs.filter(r => r.win).length
  const winRate = totalRuns > 0 ? Math.round((wins / totalRuns) * 100) : 0
  const avgFloors = totalRuns > 0 ? Math.round(runs.reduce((s, r) => s + r.total_floors, 0) / totalRuns) : 0
  const recentRuns = runs.slice(0, 10)

  const stats = [
    { label: 'TOTAL RUNS', value: totalRuns, icon: Flame },
    { label: 'WIN RATE', value: `${winRate}%`, icon: Trophy },
    { label: 'AVG FLOORS', value: avgFloors, icon: Map },
    { label: 'TOTAL COMBATS', value: totalCombats, icon: Swords },
  ]

  return (
    <div>
      <h1 className="font-[family-name:var(--font-display)] text-3xl font-bold text-[var(--gold-light)] mb-2">Dashboard</h1>
      <p className="text-sm text-[var(--text-muted)] mb-8">Your Slay the Spire 2 run analytics at a glance</p>

      {/* Stat Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        {stats.map(({ label, value, icon: Icon }) => (
          <div key={label} className="glass-card p-5 relative overflow-hidden">
            <Icon className="absolute top-4 right-4 w-8 h-8 text-[var(--text-subtle)] opacity-30" />
            <div className="stat-value text-3xl font-bold text-[var(--gold-light)]">{value}</div>
            <div className="text-xs text-[var(--text-muted)] mt-1 tracking-wider font-medium">{label}</div>
          </div>
        ))}
      </div>

      {/* Recent Runs */}
      <h2 className="text-lg font-semibold text-[var(--text-primary)] mb-4">Recent Runs</h2>

      {recentRuns.length === 0 ? (
        <div className="glass-card p-10 text-center">
          <Flame className="w-10 h-10 text-[var(--text-subtle)] mx-auto mb-3" />
          <p className="text-[var(--text-muted)]">No runs yet. Start playing!</p>
        </div>
      ) : (
        <div className="flex flex-col divide-y divide-[var(--border-subtle)]">
          {recentRuns.map((run) => {
            const runPlayers = playersByRun[run.id] ?? []
            const primaryChar = runPlayers[0]?.character ?? 'Unknown'
            const config = getCharacterConfig(primaryChar)
            const charNames = runPlayers.length > 1
              ? runPlayers.map(p => getCharacterConfig(p.character).name).join(' & ')
              : config.name

            return (
              <Link key={run.id} href={`/runs/${run.id}`}>
                <div className="flex items-center gap-4 px-4 py-3 cursor-pointer transition-colors hover:bg-[var(--bg-card-hover)] rounded-[var(--radius-md)]">
                  <div
                    className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                    style={{ backgroundColor: config.color }}
                  />
                  <div className="flex-1 min-w-0">
                    <span className="font-semibold text-sm text-[var(--text-primary)]">{charNames}</span>
                    <span className="text-[var(--gold)] text-xs font-bold ml-2">A{run.ascension}</span>
                  </div>
                  <StatusBadge win={run.win} abandoned={run.abandoned} />
                  <div className="flex items-center gap-3 text-xs text-[var(--text-muted)]">
                    <span className="flex items-center gap-1"><Map className="w-3 h-3" />{run.total_floors} floors</span>
                    <span className="flex items-center gap-1"><Clock className="w-3 h-3" />{formatTimeAgo(run.started_at)}</span>
                  </div>
                </div>
              </Link>
            )
          })}
        </div>
      )}
    </div>
  )
}
