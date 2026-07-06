import type { LogEventName, T2WebLog } from '../types'

export const LOG_STORAGE_KEY = 't2_web_logs'

export function getLogs(): T2WebLog[] {
  try {
    const stored = localStorage.getItem(LOG_STORAGE_KEY)
    if (!stored) return []

    const parsed: unknown = JSON.parse(stored)
    return Array.isArray(parsed) ? (parsed as T2WebLog[]) : []
  } catch {
    return []
  }
}

export function logEvent(
  event: LogEventName,
  payload: Record<string, unknown> = {},
): void {
  const entry: T2WebLog = {
    timestamp: new Date().toISOString(),
    event,
    payload,
  }

  try {
    localStorage.setItem(LOG_STORAGE_KEY, JSON.stringify([...getLogs(), entry]))
  } catch (error) {
    console.warn('T2 Webログを保存できませんでした。', error)
  }
}

export function exportLogs(): void {
  const blob = new Blob([JSON.stringify(getLogs(), null, 2)], {
    type: 'application/json',
  })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = 'logs.json'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(url)
}
