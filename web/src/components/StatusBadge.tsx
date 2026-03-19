export function StatusBadge({ win, abandoned }: { win: boolean; abandoned?: boolean }) {
  if (abandoned) return <span className="badge badge-abandoned">ABANDONED</span>
  return win ? <span className="badge badge-victory">VICTORY</span> : <span className="badge badge-defeat">DEFEAT</span>
}
