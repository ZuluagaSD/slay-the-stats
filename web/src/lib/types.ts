export interface Run {
  id: string
  user_id: string | null
  seed: string | null
  ascension: number
  win: boolean
  abandoned: boolean
  killed_by_encounter: string | null
  killed_by_event: string | null
  run_time_seconds: number
  total_floors: number
  event_count: number
  started_at: string | null
  ended_at: string | null
  created_at: string
}

export interface RunPlayer {
  id: number
  run_id: string
  net_id: number
  character: string
}

export interface Combat {
  id: string
  run_id: string
  encounter_id: string | null
  victory: boolean | null
  final_round: number | null
  started_at: string | null
  ended_at: string | null
}

export interface GameEvent {
  id: number
  run_id: string
  seq: number
  type: string
  ts: string
  data: Record<string, any>
}

export interface DeckSnapshot {
  id: number
  run_id: string
  trigger: string
  ts: string
  players: Array<{
    netId: number
    character: string
    deck: Array<{ id: string; upgradeLevel: number }>
    relics: string[]
    potions: string[]
  }>
}

export interface Profile {
  id: string
  display_name: string | null
  created_at: string
  updated_at: string
}

// Character display mapping
export const CHARACTER_CONFIG: Record<string, { name: string; color: string; gradient: string }> = {
  SILENT: { name: 'The Silent', color: '#22c55e', gradient: 'from-green-600 to-emerald-800' },
  IRONCLAD: { name: 'Ironclad', color: '#ef4444', gradient: 'from-red-600 to-red-900' },
  DEFECT: { name: 'Defect', color: '#3b82f6', gradient: 'from-blue-500 to-indigo-800' },
  WATCHER: { name: 'Watcher', color: '#a855f7', gradient: 'from-purple-500 to-violet-800' },
  DEPRIVED: { name: 'Deprived', color: '#f59e0b', gradient: 'from-amber-500 to-orange-800' },
  NECROBINDER: { name: 'Necrobinder', color: '#a855f7', gradient: 'from-purple-500 to-violet-800' },
}

export function getCharacterConfig(character: string) {
  return CHARACTER_CONFIG[character] || { name: character, color: '#9ca3af', gradient: 'from-gray-500 to-gray-800' }
}

export function formatDuration(seconds: number): string {
  if (!seconds || seconds <= 0) return '—'
  const m = Math.floor(seconds / 60)
  const s = Math.floor(seconds % 60)
  return `${m}:${s.toString().padStart(2, '0')}`
}

export function parseActorName(actor: string): { character: string; playerTag: string | null } {
  const colonIdx = actor.indexOf(':')
  if (colonIdx > 0) {
    return { character: actor.slice(0, colonIdx), playerTag: actor.slice(colonIdx + 1) }
  }
  // Handle #NNNN format too
  const hashIdx = actor.indexOf('#')
  if (hashIdx > 0) {
    return { character: actor.slice(0, hashIdx), playerTag: actor.slice(hashIdx + 1) }
  }
  return { character: actor, playerTag: null }
}

export function displayActor(actor: string, multiPlayer: boolean): string {
  const { character, playerTag } = parseActorName(actor)
  if (multiPlayer && playerTag) return `${playerTag}`
  const config = CHARACTER_CONFIG[character]
  return config ? config.name : character
}

export function formatDurationLong(seconds: number): string {
  if (!seconds || seconds <= 0) return '—'
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = Math.floor(seconds % 60)
  if (h > 0) return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`
  return `${m}:${s.toString().padStart(2, '0')}`
}

export function formatTimeAgo(dateStr: string | null): string {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - d.getTime()
  const diffMins = Math.floor(diffMs / 60000)
  if (diffMins < 60) return `${diffMins}m ago`
  const diffHours = Math.floor(diffMins / 60)
  if (diffHours < 24) return `${diffHours}h ago`
  const diffDays = Math.floor(diffHours / 24)
  return `${diffDays}d ago`
}
