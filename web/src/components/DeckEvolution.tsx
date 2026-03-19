import { useState } from 'react'
import { DeckSnapshot } from '@/lib/types'

function formatCardName(id: string): string {
  return id
    .replace(/_/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase())
}

// Simple heuristic for card type based on common StS card name patterns
function guessCardType(id: string): string {
  const lower = id.toLowerCase()
  if (lower.includes('strike') || lower.includes('bash') || lower.includes('carnage') ||
      lower.includes('slash') || lower.includes('flick') || lower.includes('throw') ||
      lower.includes('blade') || lower.includes('bite') || lower.includes('claw') ||
      lower.includes('ricochet') || lower.includes('poison') || lower.includes('gamble')) {
    return 'attack'
  }
  if (lower.includes('defend') || lower.includes('block') || lower.includes('survivor') ||
      lower.includes('reflex') || lower.includes('dodge') || lower.includes('neutralize') ||
      lower.includes('acrobatics') || lower.includes('trick') || lower.includes('greed') ||
      lower.includes('untouchable') || lower.includes('speedster') || lower.includes('echoing')) {
    return 'skill'
  }
  if (lower.includes('power') || lower.includes('form') || lower.includes('grasp') ||
      lower.includes('demon') || lower.includes('bane') || lower.includes('ascender')) {
    return 'power'
  }
  if (lower.includes('curse') || lower.includes('regret') || lower.includes('pain') ||
      lower.includes('decay') || lower.includes('doubt') || lower.includes('shame')) {
    return 'curse'
  }
  if (lower.includes('wound') || lower.includes('burn') || lower.includes('dazed') ||
      lower.includes('void') || lower.includes('slime')) {
    return 'status'
  }
  return 'skill'
}

const TYPE_STYLES: Record<string, { border: string; glow: string; nameBg: string; label: string }> = {
  attack: { border: '#c0392b', glow: 'rgba(192,57,43,0.4)', nameBg: 'linear-gradient(180deg, rgba(120,20,20,0.95), rgba(80,10,10,0.98))', label: 'ATK' },
  skill: { border: '#2471a3', glow: 'rgba(36,113,163,0.4)', nameBg: 'linear-gradient(180deg, rgba(20,60,110,0.95), rgba(10,35,75,0.98))', label: 'SKL' },
  power: { border: '#8e44ad', glow: 'rgba(142,68,173,0.4)', nameBg: 'linear-gradient(180deg, rgba(70,20,110,0.95), rgba(45,10,75,0.98))', label: 'PWR' },
  curse: { border: '#555', glow: 'rgba(80,80,80,0.3)', nameBg: 'linear-gradient(180deg, rgba(40,38,36,0.95), rgba(25,23,22,0.98))', label: 'CRS' },
  status: { border: '#555', glow: 'rgba(80,80,80,0.3)', nameBg: 'linear-gradient(180deg, rgba(40,38,36,0.95), rgba(25,23,22,0.98))', label: 'STS' },
}

function CardThumbnail({ card }: { card: { id: string; upgradeLevel: number } }) {
  const [imgFailed, setImgFailed] = useState(false)
  const cardType = guessCardType(card.id)
  const style = TYPE_STYLES[cardType] || TYPE_STYLES.skill
  const isUpgraded = card.upgradeLevel > 0
  const borderColor = isUpgraded ? '#d4a843' : style.border

  if (imgFailed) {
    return (
      <span className={`card-chip card-chip-${cardType}`}>
        {formatCardName(card.id)}
        {isUpgraded && <span className="card-chip-upgrade">+{card.upgradeLevel}</span>}
      </span>
    )
  }

  return (
    <div
      className="flex-shrink-0 cursor-pointer transition-all duration-200 hover:scale-110 hover:z-10 relative group"
      style={{ width: 105 }}
    >
      <div
        className="rounded-lg overflow-hidden"
        style={{
          border: `2px solid ${borderColor}`,
          boxShadow: isUpgraded
            ? '0 0 12px rgba(212,168,67,0.5), 0 4px 12px rgba(0,0,0,0.6)'
            : `0 0 8px ${style.glow}, 0 4px 12px rgba(0,0,0,0.6)`,
          background: '#0a0a14',
        }}
      >
        {/* Card art */}
        <img
          src={`/game-assets/cards/${card.id.toLowerCase()}.png`}
          alt={card.id}
          width={105}
          height={80}
          className="w-full h-auto block"
          onError={() => setImgFailed(true)}
        />

        {/* Name banner */}
        <div
          className="px-1 py-[5px] text-center border-t"
          style={{ background: style.nameBg, borderColor }}
        >
          <div
            className="text-[9px] font-bold tracking-wide truncate leading-tight"
            style={{ color: isUpgraded ? '#f0d078' : '#e8e4dc' }}
          >
            {formatCardName(card.id)}
          </div>
          <div className="text-[7px] tracking-widest mt-[1px]" style={{ color: borderColor, opacity: 0.8 }}>
            {style.label}
          </div>
        </div>
      </div>

      {/* Upgrade badge */}
      {isUpgraded && (
        <div
          className="absolute -top-1 -right-1 w-5 h-5 rounded-full flex items-center justify-center text-[8px] font-black z-10"
          style={{ background: 'linear-gradient(135deg, #f0d078, #d4a843)', color: '#1a1000', boxShadow: '0 0 6px rgba(212,168,67,0.7)' }}
        >
          +{card.upgradeLevel}
        </div>
      )}
    </div>
  )
}

export function DeckEvolution({ snapshots }: { snapshots: DeckSnapshot[] }) {
  if (snapshots.length === 0) {
    return (
      <p className="text-[var(--text-muted)] text-sm">No deck snapshots recorded.</p>
    )
  }

  // Show the latest snapshot by default
  const latest = snapshots[snapshots.length - 1]

  return (
    <div className="flex flex-col gap-5">
      {latest.players.map((player, pi) => (
        <div key={pi} className="flex flex-col gap-4">
          {/* Player label (only if multi-player or multiple entries) */}
          {latest.players.length > 1 && (
            <h4 className="text-xs font-semibold text-[var(--text-muted)] uppercase tracking-wide">
              {player.character}
            </h4>
          )}

          {/* Card Snapshots subtitle */}
          <div>
            <h5 className="text-xs text-[var(--text-subtle)] mb-2">
              Card Snapshots
            </h5>
            <div className="flex flex-wrap gap-2">
              {player.deck.map((card, ci) => (
                <CardThumbnail key={`${card.id}-${ci}`} card={card} index={ci} />
              ))}
            </div>
          </div>

          {/* Relics */}
          {player.relics.length > 0 && (
            <div>
              <h5 className="text-xs text-[var(--text-subtle)] mb-2">
                Relics
              </h5>
              <div className="flex flex-wrap gap-1.5">
                {player.relics.map((relic, ri) => (
                  <span key={`${relic}-${ri}`} className="relic-chip">
                    <span className="relic-icon" />
                    {formatCardName(relic)}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Potions */}
          {player.potions.filter((p) => p.toLowerCase() !== 'empty').length > 0 && (
            <div>
              <h5 className="text-xs text-[var(--text-subtle)] mb-2">Potions</h5>
              <div className="flex flex-wrap gap-1.5">
                {player.potions
                  .filter((p) => p.toLowerCase() !== 'empty')
                  .map((potion, poi) => (
                    <span key={`${potion}-${poi}`} className="potion-chip">
                      <img
                        src={`/game-assets/potions/${potion.toLowerCase()}.webp`}
                        alt={potion}
                        width={28}
                        height={28}
                        className="potion-img"
                        onError={(e) => { (e.target as HTMLImageElement).style.display = 'none' }}
                      />
                      {formatCardName(potion)}
                    </span>
                  ))}
              </div>
            </div>
          )}
        </div>
      ))}
    </div>
  )
}
