export function HPBar({
  current,
  max,
  variant,
}: {
  current: number
  max: number
  variant: 'player' | 'enemy'
}) {
  const pct = max > 0 ? Math.max(0, Math.min(100, (current / max) * 100)) : 0

  return (
    <div className="hp-bar-track w-full">
      <div
        className={`hp-bar-fill ${variant === 'player' ? 'hp-bar-player' : 'hp-bar-enemy'}`}
        style={{ width: `${pct}%` }}
      />
    </div>
  )
}
