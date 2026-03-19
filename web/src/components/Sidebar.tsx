'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { LayoutDashboard, ScrollText, LogIn, Swords } from 'lucide-react'

const NAV_ITEMS = [
  { href: '/', label: 'Dashboard', icon: LayoutDashboard },
  { href: '/runs', label: 'Run History', icon: ScrollText },
] as const

export function Sidebar() {
  const pathname = usePathname()

  return (
    <aside className="fixed left-0 top-0 h-screen w-16 lg:w-56 bg-[var(--bg-secondary)] border-r border-[var(--border-subtle)] flex flex-col z-50">
      {/* Logo */}
      <div className="flex items-center gap-3 px-4 py-5 border-b border-[var(--border-subtle)]">
        <Swords className="w-6 h-6 text-[var(--gold)]" />
        <span className="hidden lg:block font-[family-name:var(--font-display)] text-[var(--gold-light)] text-lg font-bold">
          Slay the Stats
        </span>
      </div>

      {/* Navigation */}
      <nav className="flex-1 flex flex-col gap-1 p-2">
        {NAV_ITEMS.map(({ href, label, icon: Icon }) => {
          const isActive = pathname === href || (href !== '/' && pathname.startsWith(href))
          return (
            <Link
              key={href}
              href={href}
              className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm transition-colors ${
                isActive
                  ? 'bg-[rgba(212,168,67,0.12)] text-[var(--gold-light)] font-semibold'
                  : 'text-[var(--text-muted)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card-hover)]'
              }`}
            >
              <Icon className={`w-5 h-5 flex-shrink-0 ${isActive ? 'text-[var(--gold)]' : ''}`} />
              <span className="hidden lg:block">{label}</span>
            </Link>
          )
        })}
      </nav>

      {/* Sign In */}
      <div className="p-2 border-t border-[var(--border-subtle)]">
        <Link
          href="/sign-in"
          className="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm text-[var(--text-muted)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card-hover)] transition-colors"
        >
          <LogIn className="w-5 h-5 flex-shrink-0" />
          <span className="hidden lg:block">Sign In</span>
        </Link>
      </div>
    </aside>
  )
}
