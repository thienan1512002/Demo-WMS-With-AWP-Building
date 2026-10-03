import { render, screen, cleanup } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { BackendStatus } from './BackendStatus'

afterEach(() => { cleanup(); vi.unstubAllGlobals() })

test('reports a healthy API connection', async () => {
  const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ status: 'Healthy' }) })
  vi.stubGlobal('fetch', fetch)
  render(<BackendStatus />)
  expect(await screen.findByText('Đã kết nối')).toBeInTheDocument()
  expect(fetch.mock.calls[0][0]).toBe('/api/health')
})

test('reports an unavailable API without presenting a false healthy state', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }))
  render(<BackendStatus />)
  expect(await screen.findByText('Chưa kết nối')).toBeInTheDocument()
})

test('rejects unexpected health responses', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ status: 'Unhealthy' }) }))
  render(<BackendStatus />)
  expect(await screen.findByText('Chưa kết nối')).toBeInTheDocument()
})
