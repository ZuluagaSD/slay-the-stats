import { GameEvent, displayActor } from '@/lib/types'

interface EventConfig {
  icon: string
  color: string
  describe: (data: Record<string, unknown>, multiPlayer: boolean) => React.ReactNode
}

function HighlightNum({ children, color }: { children: React.ReactNode; color?: string }) {
  return <span style={{ color: color ?? 'var(--ev-damage)', fontWeight: 600 }}>{children}</span>
}

function HighlightName({ children, color }: { children: React.ReactNode; color?: string }) {
  return <span style={{ color: color ?? '#fb923c', fontWeight: 500 }}>{children}</span>
}

function CardName({ children }: { children: React.ReactNode }) {
  return <span style={{ color: 'var(--ev-card)', fontWeight: 500 }}>{children}</span>
}

function PowerName({ children }: { children: React.ReactNode }) {
  return <span style={{ color: 'var(--ev-power)', fontWeight: 500 }}>{children}</span>
}

function CardHover({ cardId, children }: { cardId: string; children: React.ReactNode }) {
  const name = cardId.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase())
  return (
    <span className="relative group inline-block">
      {children}
      <div
        className="hidden group-hover:block absolute bottom-full left-1/2 -translate-x-1/2 mb-3 z-50 pointer-events-none"
        style={{ width: 220 }}
      >
        <div
          className="rounded-xl overflow-hidden"
          style={{
            border: '2px solid rgba(212,168,67,0.6)',
            boxShadow: '0 0 20px rgba(212,168,67,0.3), 0 8px 32px rgba(0,0,0,0.8)',
            background: '#0a0a14',
          }}
        >
          <img
            src={`/game-assets/cards/${cardId.toLowerCase()}.png`}
            width={220}
            height={167}
            className="w-full block"
            alt={cardId}
            onError={(e) => {
              const parent = (e.target as HTMLImageElement).closest('[class*="group-hover"]') as HTMLElement
              if (parent) parent.style.display = 'none'
            }}
          />
          <div className="px-3 py-2 text-center" style={{ background: 'linear-gradient(180deg, rgba(20,18,30,0.95), rgba(10,9,15,0.98))' }}>
            <div className="text-sm font-bold" style={{ color: '#f0d078' }}>{name}</div>
          </div>
        </div>
      </div>
    </span>
  )
}

const EVENT_MAP: Record<string, EventConfig> = {
  card_played: {
    icon: '\uD83C\uDCB4',
    color: 'var(--ev-card)',
    describe: (d, mp) => {
      const cardId = String(d.cardId ?? 'unknown')
      const target = d.target ? <> -&gt; <HighlightName>{displayActor(String(d.target), mp)}</HighlightName></> : null
      return <>Played <CardHover cardId={cardId}><CardName>{cardId}</CardName></CardHover>{target}</>
    },
  },
  damage_received: {
    icon: '\u2694',
    color: 'var(--ev-damage)',
    describe: (d, mp) => {
      const receiver = d.receiver ? displayActor(String(d.receiver), mp) : 'target'
      const dealer = d.dealer ? displayActor(String(d.dealer), mp) : ''
      const unblocked = Number(d.unblockedDamage ?? 0)
      const blocked = Number(d.blockedDamage ?? 0)
      const blockedStr = blocked > 0 ? <> (<HighlightNum color="var(--ev-block)">{blocked}</HighlightNum> blocked)</> : null
      const side = String(d.side ?? '').toLowerCase()
      // On enemy turn, show "{receiver} takes N damage" style
      if (side === 'enemy' || side === 'enemies') {
        return <><HighlightName>{receiver}</HighlightName> takes <HighlightNum>{unblocked}</HighlightNum> damage{blockedStr}</>
      }
      return <>Dealt <HighlightNum>{unblocked}</HighlightNum> damage to <HighlightName>{receiver}</HighlightName>{blockedStr}</>
    },
  },
  block_gained: {
    icon: '\uD83D\uDEE1',
    color: 'var(--ev-block)',
    describe: (d) => <>Gained <HighlightNum color="var(--ev-block)">{Number(d.amount ?? 0)}</HighlightNum> Block</>,
  },
  power_received: {
    icon: '\u2727',
    color: 'var(--ev-power)',
    describe: (d, mp) => {
      const powerId = String(d.powerId ?? 'power')
      const amount = d.amount != null ? ` x${d.amount}` : ''
      const applier = d.applier ? <> from <HighlightName color="var(--text-muted)">{displayActor(String(d.applier), mp)}</HighlightName></> : null
      return <>Played <img src={`/game-assets/powers/${powerId.toLowerCase()}.png`} width={16} height={16} className="inline-block align-middle" style={{display:'inline'}} onError={e => { (e.target as HTMLImageElement).style.display='none' }} alt="" />{' '}<PowerName>{powerId}{amount}</PowerName> (Power){applier}</>
    },
  },
  card_drawn: {
    icon: '\u21B3',
    color: 'var(--ev-draw)',
    describe: (d) => {
      const cardId = String(d.cardId ?? 'unknown')
      return <>Drew <CardHover cardId={cardId}><span className="text-[var(--text-primary)] cursor-pointer">{cardId}</span></CardHover></>
    },
  },
  cards_drawn_batch: {
    icon: '\u21B3',
    color: 'var(--ev-draw)',
    describe: (d) => <>Drew <HighlightNum color="var(--text-primary)">{Number(d.count ?? 0)}</HighlightNum> cards</>,
  },
  card_exhausted: {
    icon: '\uD83D\uDD25',
    color: 'var(--ev-exhaust)',
    describe: (d) => {
      const cardId = String(d.cardId ?? 'unknown')
      return <>Exhausted <CardHover cardId={cardId}><span className="text-[var(--ev-exhaust)] cursor-pointer">{cardId}</span></CardHover></>
    },
  },
  card_discarded: {
    icon: '\u21A9',
    color: 'var(--ev-draw)',
    describe: (d) => {
      const cardId = String(d.cardId ?? 'unknown')
      return <>Discarded <CardHover cardId={cardId}><span className="text-[var(--text-primary)] cursor-pointer">{cardId}</span></CardHover></>
    },
  },
  card_generated: {
    icon: '\u2795',
    color: 'var(--ev-generate)',
    describe: (d) => {
      const cardId = String(d.cardId ?? 'unknown')
      return <>Generated <CardHover cardId={cardId}><CardName>{cardId}</CardName></CardHover></>
    },
  },
  monster_move: {
    icon: '\u2620',
    color: 'var(--ev-monster)',
    describe: (d) => {
      const monsterId = String(d.monsterId ?? 'Monster')
      const moveId = String(d.moveId ?? 'move')
      return <><HighlightName color="var(--ev-monster)">{monsterId}</HighlightName> uses <span className="text-[var(--text-primary)]">{moveId}</span></>
    },
  },
  energy_spent: {
    icon: '\u26A1',
    color: 'var(--ev-energy)',
    describe: (d) => <>Spent <HighlightNum color="var(--ev-energy)">{Number(d.amount ?? 0)}</HighlightNum> energy</>,
  },
  potion_used: {
    icon: '\uD83E\uDDEA',
    color: 'var(--ev-potion)',
    describe: (d) => {
      const potionId = String(d.potionId ?? 'potion')
      return <>Used <img src={`/game-assets/potions/${potionId.toLowerCase()}.webp`} width={16} height={16} className="inline-block align-middle" style={{display:'inline'}} onError={e => { (e.target as HTMLImageElement).style.display='none' }} alt="" />{' '}<span style={{ color: 'var(--ev-potion)', fontWeight: 500 }}>{potionId}</span></>
    },
  },
  creature_attacked: {
    icon: '\uD83D\uDCA5',
    color: 'var(--ev-damage)',
    describe: (d, mp) => {
      const attacker = d.attacker ? displayActor(String(d.attacker), mp) : 'Creature'
      const wasKilled = d.wasTargetKilled ? <span className="text-red-500 font-bold"> (KILLED)</span> : null
      return <><HighlightName>{attacker}</HighlightName> attacked{wasKilled}</>
    },
  },
  orb_channeled: {
    icon: '\uD83D\uDD2E',
    color: '#06b6d4',
    describe: (d) => <>Channeled <span style={{ color: '#06b6d4' }}>{String(d.orbId ?? 'orb')}</span></>,
  },
  card_afflicted: {
    icon: '\u2623',
    color: '#eab308',
    describe: (d) => {
      const afflictionId = String(d.afflictionId ?? 'affliction')
      return <>Afflicted with <span style={{ color: '#eab308' }}>{afflictionId}</span></>
    },
  },
  stars_modified: {
    icon: '\u2B50',
    color: 'var(--gold)',
    describe: (d) => {
      const amount = Number(d.amount ?? 0)
      return amount >= 0
        ? <>Gained <HighlightNum color="var(--gold)">{amount}</HighlightNum> stars</>
        : <>Lost <HighlightNum color="var(--defeat)">{Math.abs(amount)}</HighlightNum> stars</>
    },
  },
  summoned: {
    icon: '\uD83D\uDC64',
    color: '#eab308',
    describe: (d) => <>Summoned <HighlightName color="#eab308">{String(d.monsterId ?? d.name ?? 'creature')}</HighlightName></>,
  },
}

const FALLBACK_CONFIG: EventConfig = {
  icon: '\u2022',
  color: 'var(--text-muted)',
  describe: (d) => {
    const keys = Object.keys(d).filter((k) => k !== 'combatId' && k !== 'round' && k !== 'side')
    if (keys.length > 0) return <>{keys.map((k) => `${k}=${String(d[k])}`).join(', ')}</>
    return <>event</>
  },
}

export function EventLine({
  event,
  multiPlayer,
}: {
  event: GameEvent
  multiPlayer: boolean
}) {
  const config = EVENT_MAP[event.type] ?? FALLBACK_CONFIG
  const data = (event.data ?? {}) as Record<string, unknown>
  const description = config.describe(data, multiPlayer)

  // In single-player, suppress actor name on the player's turn (it's redundant)
  const side = String(data.side ?? '').toLowerCase()
  const isPlayerTurn = side === 'player' || side === 'players'
  const showActor = multiPlayer || !isPlayerTurn
  const actor = showActor && data.actor ? displayActor(String(data.actor), multiPlayer) : null

  return (
    <div className="event-line flex items-start gap-1">
      <span className="event-icon" style={{ color: config.color }}>
        [{config.icon}]
      </span>
      <span className="text-[var(--text-muted)]">
        {actor && (
          <span className="text-[var(--text-primary)] font-medium">{actor} </span>
        )}
        {description}
      </span>
    </div>
  )
}
