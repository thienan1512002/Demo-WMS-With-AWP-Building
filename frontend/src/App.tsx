import { useEffect, useState } from 'react'
import { Catalog, type CatalogKind } from './Catalog'
import { BackendStatus } from './BackendStatus'
import './App.css'

export default function App() {
  const [route, setRoute] = useState(window.location.hash)
  useEffect(() => {
    const navigate = () => setRoute(window.location.hash)
    window.addEventListener('hashchange', navigate)
    return () => window.removeEventListener('hashchange', navigate)
  }, [])
  const kind: CatalogKind | null = route === '#/goods' ? 'goods' : route === '#/warehouses' ? 'warehouses' : null
  return <div className="workspace">
    <header className="topbar"><a href="#/" className="brand" aria-label="Warehouse — Trang chủ"><span className="brand-mark" aria-hidden="true">W</span>Warehouse</a><BackendStatus /></header>
    <main>
      <p className="eyebrow">WORKSPACE</p><h1>Quản lý kho</h1>
      <p className="intro">Theo dõi hàng hóa, hoạt động nhập xuất và tồn kho trong một không gian làm việc.</p>
      <nav className="catalog-menu" aria-label="Danh mục"><a href="#/" aria-current={!kind ? 'page' : undefined}>Trang chủ</a><a href="#/goods" aria-current={kind === 'goods' ? 'page' : undefined}>Hàng hóa</a><a href="#/warehouses" aria-current={kind === 'warehouses' ? 'page' : undefined}>Kho</a></nav>
      {kind ? <Catalog key={kind} kind={kind} /> : <section aria-labelledby="modules-heading"><h2 id="modules-heading">Không gian quản lý</h2><div className="modules">
        <article><span className="module-number">01</span><h3>Danh mục hàng hóa</h3><p>Tổ chức hàng hóa và các kho lưu trữ.</p><a className="module-state" href="#/goods">Quản lý hàng hóa</a> <a className="module-state" href="#/warehouses">Quản lý kho</a></article>
        <article><span className="module-number">02</span><h3>Nhập và xuất kho</h3><p>Ghi nhận giao dịch, số lượng và lịch sử.</p><span className="module-state">Chưa triển khai</span></article>
        <article><span className="module-number">03</span><h3>Tồn kho và báo cáo</h3><p>Kiểm soát số dư và theo dõi biến động kho.</p><span className="module-state">Chưa triển khai</span></article>
      </div></section>}
    </main><footer>Warehouse workspace</footer>
  </div>
}
