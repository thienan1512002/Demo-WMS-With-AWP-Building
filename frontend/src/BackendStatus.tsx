import { useEffect, useState } from 'react'

export function BackendStatus() {
  const [status, setStatus] = useState<'loading' | 'ready' | 'offline'>('loading')
  useEffect(() => {
    const controller = new AbortController()
    fetch('/api/health', { signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error('API unavailable')
        const health: { status?: string } = await response.json()
        if (!controller.signal.aborted) setStatus(health.status === 'Healthy' ? 'ready' : 'offline')
      })
      .catch(() => { if (!controller.signal.aborted) setStatus('offline') })
    return () => controller.abort()
  }, [])
  const labels = { loading: 'Đang kết nối', ready: 'Đã kết nối', offline: 'Chưa kết nối' }
  return <span className={`connection connection--${status}`} role="status">{labels[status]}</span>
}
