import { GameEvent, CHARACTER_CONFIG } from '@/lib/types'

interface CombatStats {
  damageDealt: number
  damageTaken: number
  blockGenerated: number
  cardsPlayed: number
  energySpent: number
}

function isPlayerCharacter(name: string): boolean {
  return name in CHARACTER_CONFIG
}

function computeStats(events: GameEvent[]): CombatStats {
  const stats: CombatStats = {
    damageDealt: 0,
    damageTaken: 0,
    blockGenerated: 0,
    cardsPlayed: 0,
    energySpent: 0,
  }

  for (const ev of events) {
    const data = ev.data ?? {}
    switch (ev.type) {
      case 'damage_received': {
        const unblocked = Number(data.unblockedDamage ?? 0)
        const dealer = String(data.dealer ?? '')
        // If the dealer is a known player character, it's outgoing damage
        if (isPlayerCharacter(dealer)) {
          stats.damageDealt += unblocked
        } else {
          stats.damageTaken += unblocked
        }
        break
      }
      case 'block_gained':
        stats.blockGenerated += Number(data.amount ?? 0)
        break
      case 'card_played':
        stats.cardsPlayed += 1
        break
      case 'energy_spent':
        stats.energySpent += Number(data.amount ?? 0)
        break
    }
  }

  return stats
}

const STAT_ROWS: { label: string; key: keyof CombatStats }[] = [
  { label: 'Total Damage', key: 'damageDealt' },
  { label: 'Damage Taken', key: 'damageTaken' },
  { label: 'Block Generated', key: 'blockGenerated' },
]

export function DamageStatsTable({ events }: { events: GameEvent[] }) {
  const stats = computeStats(events)

  return (
    <div className="flex flex-col gap-1">
      <p className="text-xs text-[var(--text-subtle)] mb-2">Per combat summary</p>
      <table className="stats-table">
        <thead>
          <tr>
            <th></th>
            <th>Total damage</th>
            <th>Damagerated</th>
          </tr>
        </thead>
        <tbody>
          {STAT_ROWS.map(({ label, key }) => (
            <tr key={key}>
              <td>{label}</td>
              <td>{stats[key].toLocaleString()}</td>
              <td>{Math.round(stats[key] * 0.32).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
