import type { Job, JobStatus } from './types'

const formatter = new Intl.DateTimeFormat('ru-RU', {
  dateStyle: 'short',
  timeStyle: 'medium',
})

export const statusStyles: Record<JobStatus, string> = {
  Created: 'bg-slate-900 text-white',
  Queued: 'bg-amber-200 text-amber-950',
  Running: 'bg-cyan-200 text-cyan-950',
  Succeeded: 'bg-emerald-200 text-emerald-950',
  Failed: 'bg-rose-200 text-rose-950',
  Cancelled: 'bg-stone-300 text-stone-900',
  TimedOut: 'bg-orange-200 text-orange-950',
}

export function sortJobs(jobs: Job[]): Job[] {
  return [...jobs].sort((left, right) => {
    return new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
  })
}

export function isTerminalStatus(status: JobStatus): boolean {
  return ['Succeeded', 'Failed', 'Cancelled', 'TimedOut'].includes(status)
}

export function shortId(id: string): string {
  if (id.length < 14) {
    return id
  }

  return `${id.slice(0, 8)}...${id.slice(-4)}`
}

export function formatDate(value: string | null): string {
  if (!value) {
    return '—'
  }

  return formatter.format(new Date(value))
}

export function formatPercent(progress: number): string {
  return `${Math.round(progress * 100)}%`
}

export function normalizeUuid(value: string): string {
  return value.trim()
}
