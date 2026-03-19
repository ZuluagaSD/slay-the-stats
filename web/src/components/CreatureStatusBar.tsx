import { HPBar } from '@/components/HPBar'
import { getCharacterConfig } from '@/lib/types'

interface CreatureData {
  name?: unknown
  hp?: unknown
  currentHp?: unknown
  maxHp?: unknown
  [key: string]: unknown
}

function formatCreatureName(rawName: string): string {
  // Check if it's a known player character
  const config = getCharacterConfig(rawName)
  if (config.name !== rawName) return config.name

  // Convert SPINY_TOAD -> Spiny Toad
  return rawName
    .replace(/_/g, ' ')
    .replace(/\b\w+/g, (w) => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase())
}

function safeCreature(raw: unknown): { name: string; currentHp: number; maxHp: number } {
  const obj = (raw && typeof raw === 'object' ? raw : {}) as CreatureData
  return {
    name: formatCreatureName(String(obj.name ?? 'Unknown')),
    currentHp: Number(obj.currentHp ?? obj.hp ?? 0),
    maxHp: Number(obj.maxHp ?? 1),
  }
}

function CreatureCard({
  creature,
  variant,
}: {
  creature: { name: string; currentHp: number; maxHp: number }
  variant: 'player' | 'enemy'
}) {
  const isDead = creature.currentHp <= 0
  const nameColor = variant === 'player' ? 'text-green-400' : 'text-red-400'
  const hpColor = isDead ? 'text-red-500' : 'text-[var(--text-muted)]'

  return (
    <div className="creature-card">
      {/* Portrait placeholder */}
      <div className={`creature-portrait ${variant === 'player' ? 'creature-portrait-player' : 'creature-portrait-enemy'}`}>
        <span className="creature-portrait-letter">
          {creature.name.charAt(0)}
        </span>
      </div>

      {/* Name + HP info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center justify-between gap-2 mb-1">
          <span className={`text-sm font-semibold ${nameColor} truncate`}>
            {creature.name}
          </span>
          <span className={`text-xs ${hpColor} flex-shrink-0 tabular-nums`}>
            {creature.currentHp}/{creature.maxHp} HP
          </span>
        </div>
        <HPBar current={creature.currentHp} max={creature.maxHp} variant={variant} />
      </div>
    </div>
  )
}

export function CreatureStatusBar({
  playerCreatures,
  enemyCreatures,
}: {
  playerCreatures: unknown[]
  enemyCreatures: unknown[]
}) {
  const players = playerCreatures.map(safeCreature)
  const enemies = enemyCreatures.map(safeCreature)

  if (players.length === 0 && enemies.length === 0) return null

  return (
    <div className="flex gap-3 flex-wrap">
      {players.map((c, i) => (
        <CreatureCard key={`player-${i}`} creature={c} variant="player" />
      ))}
      {enemies.map((c, i) => (
        <CreatureCard key={`enemy-${i}`} creature={c} variant="enemy" />
      ))}
    </div>
  )
}
