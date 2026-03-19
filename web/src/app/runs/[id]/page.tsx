import { supabase } from '@/lib/supabase'
import { Run, RunPlayer, Combat, GameEvent, DeckSnapshot, getCharacterConfig } from '@/lib/types'
import { RunDetailHeader } from '@/components/RunDetailHeader'
import { RunDetailBody } from '@/components/RunDetailBody'
import Link from 'next/link'
import { ArrowLeft, Skull } from 'lucide-react'

export const dynamic = 'force-dynamic'

export default async function RunDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params

  const [runRes, playersRes, combatsRes, eventsRes, snapshotsRes] = await Promise.all([
    supabase.from('runs').select('*').eq('id', id).single(),
    supabase.from('run_players').select('*').eq('run_id', id),
    supabase.from('combats').select('*').eq('run_id', id).order('started_at'),
    supabase.from('events').select('*').eq('run_id', id).order('seq'),
    supabase.from('deck_snapshots').select('*').eq('run_id', id).order('ts'),
  ])

  const run = runRes.data as Run | null
  const players = (playersRes.data ?? []) as RunPlayer[]
  const combats = (combatsRes.data ?? []) as Combat[]
  const events = (eventsRes.data ?? []) as GameEvent[]
  const deckSnapshots = (snapshotsRes.data ?? []) as DeckSnapshot[]

  if (!run) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4">
        <Skull className="w-12 h-12 text-[var(--defeat)]" />
        <h1 className="text-xl font-semibold text-[var(--text-primary)]">Run not found</h1>
        <Link href="/runs" className="text-[var(--gold)] hover:underline flex items-center gap-1">
          <ArrowLeft className="w-4 h-4" /> Back to runs
        </Link>
      </div>
    )
  }

  const multiPlayer = players.length > 1

  return (
    <div className="flex flex-col gap-4">
      <Link href="/runs" className="text-sm text-[var(--text-muted)] hover:text-[var(--gold)] flex items-center gap-1 w-fit">
        <ArrowLeft className="w-4 h-4" /> Back to runs
      </Link>

      <RunDetailHeader run={run} players={players} multiPlayer={multiPlayer} />

      <RunDetailBody
        combats={combats}
        events={events}
        deckSnapshots={deckSnapshots}
        multiPlayer={multiPlayer}
      />
    </div>
  )
}
