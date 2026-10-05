import { useEffect, useRef, useState } from 'react'

export type CatalogKind = 'goods' | 'warehouses'
type Item = { id: string, code: string, name: string, archivedAt: string | null }
type Page = { items: Item[], total: number, page: number, pageSize: number }
type Errors = Record<string, string[]>

async function request(path: string, options: RequestInit) {
  const response = await fetch(path, options)
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}))
    throw { message: problem.title || `Yêu cầu thất bại (${response.status}).`, errors: problem.errors || {} }
  }
  return response.json()
}

export function Catalog({ kind }: { kind: CatalogKind }) {
  const label = kind === 'goods' ? 'hàng hóa' : 'kho'
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [archived, setArchived] = useState('false')
  const [page, setPage] = useState(1)
  const [reload, setReload] = useState(0)
  const [result, setResult] = useState<{ key: string, data: Page | null, error: string } | null>(null)
  const requestKey = JSON.stringify([kind, query, archived, page, reload])
  const loading = result?.key !== requestKey
  const data = result?.data
  const error = result?.error
  const [editor, setEditor] = useState<{ item?: Item } | null>(null)
  const [notice, setNotice] = useState('')
  const createButton = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    const controller = new AbortController()
    const params = new URLSearchParams({ page: String(page), pageSize: '20', search: query })
    if (archived) params.set('archived', archived)
    request(`/api/${kind}?${params}`, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setResult({ key: requestKey, data, error: '' }) })
      .catch(failure => { if (!controller.signal.aborted) setResult({ key: requestKey, data: null, error: failure.message || 'Không thể tải danh sách. Vui lòng thử lại.' }) })
    return () => controller.abort()
  }, [kind, query, archived, page, requestKey])
  function closeEditor() {
    setEditor(null)
    createButton.current?.focus()
  }
  return <section className="catalog" aria-labelledby="catalog-title">
    <div className="catalog-heading"><h2 id="catalog-title">Danh sách {label}</h2><button ref={createButton} onClick={() => { setEditor({}); setNotice('') }}>Tạo {label}</button></div>
    {notice && <p role="status">{notice}</p>}
    <form className="catalog-filters" onSubmit={event => { event.preventDefault(); setPage(1); setQuery(search.trim()); setReload(value => value + 1) }}>
      <label>Tìm theo mã hoặc tên<input value={search} onChange={event => setSearch(event.target.value)} /></label>
      <label>Trạng thái<select value={archived} onChange={event => { setArchived(event.target.value); setPage(1) }}><option value="false">Đang hoạt động</option><option value="true">Đã lưu trữ</option><option value="">Tất cả</option></select></label>
      <button type="submit">Tìm kiếm</button>
    </form>
    {editor && <CatalogForm key={editor.item?.id || 'new'} kind={kind} item={editor.item} onCancel={closeEditor} onSaved={() => { closeEditor(); setNotice('Đã lưu thành công.'); setReload(value => value + 1) }} />}
    {loading ? <p role="status">Đang tải danh sách…</p> : error ? <div role="alert"><p>{error}</p><button onClick={() => setReload(value => value + 1)}>Thử lại</button></div> : data && <>
      {data.items.length === 0 ? <p role="status">Không có {label} phù hợp.</p> : <div className="table-scroll"><table><caption>Danh mục {label}</caption><thead><tr><th scope="col">Mã</th><th scope="col">Tên</th><th scope="col">Trạng thái</th><th scope="col">Thao tác</th></tr></thead><tbody>{data.items.map(item => <tr key={item.id}><td>{item.code}</td><td>{item.name}</td><td>{item.archivedAt ? 'Đã lưu trữ' : 'Đang hoạt động'}</td><td>{!item.archivedAt && <button aria-label={`Sửa ${item.code}`} onClick={() => { setEditor({ item }); setNotice('') }}>Sửa</button>}</td></tr>)}</tbody></table></div>}
      <nav className="pagination" aria-label="Phân trang"><button disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Trang trước</button><span>Trang {page} / {Math.max(1, Math.ceil(data.total / data.pageSize))} · {data.total} kết quả</span><button disabled={page * data.pageSize >= data.total} onClick={() => setPage(value => value + 1)}>Trang sau</button></nav>
    </>}
  </section>
}

function CatalogForm({ kind, item, onCancel, onSaved }: { kind: CatalogKind, item?: Item, onCancel: () => void, onSaved: () => void }) {
  const [code, setCode] = useState(item?.code || '')
  const [name, setName] = useState(item?.name || '')
  const [errors, setErrors] = useState<Errors>({})
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const pending = useRef(false)
  const controller = useRef<AbortController | null>(null)
  const firstInput = useRef<HTMLInputElement>(null)
  useEffect(() => { firstInput.current?.focus(); return () => controller.current?.abort() }, [])
  async function submit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending.current) return
    const invalid: Errors = {}
    if (!code.trim() || code.trim().length > 64) invalid.code = ['Mã bắt buộc và tối đa 64 ký tự.']
    if (!name.trim() || name.trim().length > 256) invalid.name = ['Tên bắt buộc và tối đa 256 ký tự.']
    setErrors(invalid)
    setError('')
    if (Object.keys(invalid).length) return
    pending.current = true
    setSaving(true)
    controller.current = new AbortController()
    try {
      await request(`/api/${kind}${item ? `/${item.id}` : ''}`, { method: item ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ code: code.trim(), name: name.trim() }), signal: controller.current.signal })
      if (!controller.current.signal.aborted) onSaved()
    } catch (failure) {
      if (!controller.current.signal.aborted) {
        const problem = failure as { message?: string, errors?: Errors }
        setError(problem.message || 'Không thể lưu. Vui lòng thử lại.')
        setErrors(problem.errors || {})
      }
    } finally { pending.current = false; setSaving(false) }
  }
  return <form className="catalog-editor" aria-labelledby="editor-title" aria-busy={saving} noValidate onSubmit={submit}>
    <h3 id="editor-title">{item ? 'Cập nhật' : 'Tạo'} {kind === 'goods' ? 'hàng hóa' : 'kho'}</h3>
    {error && <p role="alert">{error}</p>}
    <fieldset disabled={saving}><label htmlFor="catalog-code">Mã</label><input id="catalog-code" ref={!item ? firstInput : undefined} value={code} readOnly={!!item} required aria-invalid={!!errors.code} aria-describedby="code-help" onChange={event => setCode(event.target.value)} /><p id="code-help" role={errors.code ? 'alert' : undefined}>{errors.code?.join(' ') || 'Tối đa 64 ký tự. Mã không thể thay đổi sau khi tạo.'}</p>
      <label htmlFor="catalog-name">Tên</label><input id="catalog-name" ref={item ? firstInput : undefined} value={name} required aria-invalid={!!errors.name} aria-describedby="name-help" onChange={event => setName(event.target.value)} /><p id="name-help" role={errors.name ? 'alert' : undefined}>{errors.name?.join(' ') || 'Tối đa 256 ký tự.'}</p>
      <div className="editor-actions"><button type="submit">{saving ? 'Đang lưu…' : 'Lưu'}</button><button type="button" onClick={onCancel}>Hủy</button></div>
    </fieldset>
  </form>
}
