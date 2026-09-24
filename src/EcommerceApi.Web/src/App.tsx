import { useEffect, useMemo, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import "bootstrap/dist/css/bootstrap.min.css";
import { request } from "./api";
import type { Cart, Order, Product, User } from "./api";
import { catalogImageBySku, currency, demoProducts } from "./catalog";
import "./App.css";

type Page = "home" | "shop" | "orders" | "admin";
type IconName = "search" | "user" | "bag" | "arrow" | "close" | "menu" | "minus" | "plus" | "trash";
const demoMode = import.meta.env.DEV && new URLSearchParams(window.location.search).has("demo");
const emptyCart: Cart = { items: [], total: 0 };

function Icon({ name, size = 22 }: { name: IconName; size?: number }) {
  const paths: Record<IconName, ReactNode> = {
    search: <><circle cx="10.8" cy="10.8" r="6.8" /><path d="m16 16 5 5" /></>,
    user: <><circle cx="12" cy="7.5" r="3.5" /><path d="M4.5 21c.4-4.1 3-6.2 7.5-6.2s7.1 2.1 7.5 6.2" /></>,
    bag: <><path d="M4.5 8.5h15L21 21H3L4.5 8.5Z" /><path d="M8.5 9V6a3.5 3.5 0 0 1 7 0v3" /></>,
    arrow: <><path d="M3 12h17" /><path d="m14 6 6 6-6 6" /></>,
    close: <path d="M5 5 19 19M19 5 5 19" />,
    menu: <path d="M3 7h18M3 12h18M3 17h18" />,
    minus: <path d="M5 12h14" />,
    plus: <path d="M5 12h14M12 5v14" />,
    trash: <><path d="M4 7h16M9 7V4h6v3M6 7l1 14h10l1-14M10 11v6M14 11v6" /></>,
  };
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>;
}

function ProductVisual({ product }: { product: Product }) {
  const image = catalogImageBySku[product.sku];
  return image ? <img src={image} alt={product.name} loading="lazy" /> : <div className="product-no-image"><span>{product.name}</span><small>MIRA</small></div>;
}

function ProductCard({ product, onOpen, onAdd }: { product: Product; onOpen: (product: Product) => void; onAdd: (product: Product) => void }) {
  return <article className="product-card"><button className="product-photo" onClick={() => onOpen(product)} aria-label={`View ${product.name}`}><ProductVisual product={product} /></button><div className="product-meta"><button className="product-title" onClick={() => onOpen(product)}>{product.name}</button><span>{currency(product.price)}</span></div><div className="product-bottom"><span>{product.stockQuantity > 0 ? "Available now" : "Sold out"}</span><button aria-label={`Add ${product.name} to bag`} disabled={product.stockQuantity < 1} onClick={() => onAdd(product)}><Icon name="plus" size={18} /></button></div></article>;
}

function App() {
  const [page, setPage] = useState<Page>("home");
  const [products, setProducts] = useState<Product[]>(demoMode ? demoProducts : []);
  const [search, setSearch] = useState("");
  const [catalogPage, setCatalogPage] = useState(1);
  const [totalProducts, setTotalProducts] = useState(0);
  const [sort, setSort] = useState("featured");
  const [user, setUser] = useState<User | null>(null);
  const [cart, setCart] = useState<Cart>(emptyCart);
  const [orders, setOrders] = useState<Order[]>([]);
  const [cartOpen, setCartOpen] = useState(false);
  const [authOpen, setAuthOpen] = useState(false);
  const [authMode, setAuthMode] = useState<"login" | "register">("login");
  const [selected, setSelected] = useState<Product | null>(null);
  const [notice, setNotice] = useState("");
  const [loading, setLoading] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [productForm, setProductForm] = useState({ name: "", description: "", sku: "", price: "", stockQuantity: "", isActive: true });

  useEffect(() => {
    if (demoMode) return;
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      request<Product[]>(`/api/products?page=${catalogPage}&pageSize=24&sort=${sort}&search=${encodeURIComponent(search.trim())}`, { signal: controller.signal }, headers => setTotalProducts(Number(headers.get("X-Total-Count") ?? 0)))
        .then(setProducts)
        .catch((error: unknown) => { if (!controller.signal.aborted) setNotice(error instanceof Error ? error.message : "Products could not be loaded."); });
    }, search ? 250 : 0);
    return () => { controller.abort(); window.clearTimeout(timer); };
  }, [search, catalogPage, sort]);

  useEffect(() => {
    if (demoMode) return;
    localStorage.removeItem("accessToken");
    request<Omit<User, "userId"> & { id: string }>("/api/auth/me")
      .then((account) => { setUser({ ...account, userId: account.id }); return request<Cart>("/api/cart"); })
      .then(setCart)
      .catch(() => { /* An anonymous visitor has no session to restore. */ });
  }, []);

  useEffect(() => {
    const onExpired = () => {
      setUser(null);
      setCart(emptyCart);
      setNotice("Your session expired. Please sign in again.");
    };
    window.addEventListener("ecommerce:session-expired", onExpired);
    return () => window.removeEventListener("ecommerce:session-expired", onExpired);
  }, []);

  const visibleProducts = useMemo(() => {
    const list = products.filter((product) => product.isActive && (!demoMode || `${product.name} ${product.description} ${product.sku}`.toLowerCase().includes(search.toLowerCase())));
    if (sort === "low") return [...list].sort((a, b) => a.price - b.price);
    if (sort === "high") return [...list].sort((a, b) => b.price - a.price);
    if (sort === "new") return [...list].sort((a, b) => (b.createdAtUtc ?? "").localeCompare(a.createdAtUtc ?? ""));
    return list;
  }, [products, search, sort]);
  const cartCount = cart.items.reduce((sum, item) => sum + item.quantity, 0);

  function go(next: Page) { setPage(next); setMenuOpen(false); window.scrollTo({ top: 0, behavior: "smooth" }); }
  function message(error: unknown) { setNotice(error instanceof Error ? error.message : "Something went wrong."); }
  async function reloadProducts() { if (!demoMode) setProducts(await request<Product[]>("/api/products")); }

  async function submitAuth(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setLoading(true);
    try {
      const account = demoMode
        ? { userId: "demo", email: String(data.get("email")), firstName: String(data.get("firstName") || "Alex"), lastName: String(data.get("lastName") || "Customer"), role: "Customer" }
        : await request<User>(`/api/auth/session/${authMode}`, { method: "POST", body: JSON.stringify(Object.fromEntries(data)) });
      setUser(account); setAuthOpen(false); setNotice(`Welcome, ${account.firstName}.`);
      if (!demoMode) setCart(await request<Cart>("/api/cart"));
    } catch (error) { message(error); }
    finally { setLoading(false); }
  }

  async function signOut() {
    try {
      if (!demoMode) await request<void>("/api/auth/session/logout", { method: "POST" });
      sessionStorage.removeItem("mira-checkout"); setUser(null); setCart(emptyCart); setOrders([]); go("home");
    } catch (error) { message(error); }
  }

  async function addToCart(product: Product) {
    if (!user) { setAuthOpen(true); setNotice("Sign in to add items to your bag."); return; }
    setLoading(true);
    try {
      if (demoMode) setCart((current) => {
        const existing = current.items.find((item) => item.productId === product.id);
        const items = existing
          ? current.items.map((item) => item.productId === product.id ? { ...item, quantity: item.quantity + 1, lineTotal: item.unitPrice * (item.quantity + 1) } : item)
          : [...current.items, { id: product.id, productId: product.id, productName: product.name, productSku: product.sku, unitPrice: product.price, quantity: 1, lineTotal: product.price }];
        return { items, total: items.reduce((sum, item) => sum + item.lineTotal, 0) };
      });
      else setCart(await request<Cart>("/api/cart/items", { method: "POST", body: JSON.stringify({ productId: product.id, quantity: 1 }) }));
      setSelected(null); setCartOpen(true); setNotice("");
    } catch (error) { message(error); }
    finally { setLoading(false); }
  }

  async function changeQuantity(itemId: string, quantity: number) {
    if (!user || quantity < 1 || quantity > 100) return;
    try {
      if (demoMode) setCart((current) => {
        const items = current.items.map((item) => item.id === itemId ? { ...item, quantity, lineTotal: item.unitPrice * quantity } : item);
        return { items, total: items.reduce((sum, item) => sum + item.lineTotal, 0) };
      });
      else setCart(await request<Cart>(`/api/cart/items/${itemId}`, { method: "PUT", body: JSON.stringify({ quantity }) }));
    } catch (error) { message(error); }
  }

  async function removeItem(itemId: string) {
    if (!user) return;
    try {
      if (demoMode) setCart((current) => {
        const items = current.items.filter((item) => item.id !== itemId);
        return { items, total: items.reduce((sum, item) => sum + item.lineTotal, 0) };
      });
      else setCart(await request<Cart>(`/api/cart/items/${itemId}`, { method: "DELETE" }));
    } catch (error) { message(error); }
  }

  async function checkout() {
    if (!user || !cart.items.length) return;
    setLoading(true);
    try {
      if (demoMode) setOrders((current) => [{ id: `demo-${Date.now()}`, totalAmount: cart.total, status: "Pending", createdAtUtc: new Date().toISOString(), items: cart.items }, ...current]);
      else {
        const fingerprint = JSON.stringify({ userId: user.userId, items: cart.items.map(item => [item.id, item.quantity, item.unitPrice]) });
        let attempt: { key: string; fingerprint: string } | null = null;
        try { attempt = JSON.parse(sessionStorage.getItem("mira-checkout") ?? "null"); } catch { /* Replace invalid attempt state. */ }
        if (attempt?.fingerprint !== fingerprint) attempt = { key: crypto.randomUUID(), fingerprint };
        sessionStorage.setItem("mira-checkout", JSON.stringify(attempt));
        const order = await request<Order>("/api/checkout", { method: "POST", headers: { "Idempotency-Key": attempt.key }, body: JSON.stringify({ expectedTotal: cart.total }) });
        sessionStorage.removeItem("mira-checkout");
        setOrders((current) => [order, ...current]); await reloadProducts();
      }
      setCart(emptyCart); setCartOpen(false); setNotice("Simulated order created. No payment was collected."); go("orders");
    } catch (error) { message(error); }
    finally { setLoading(false); }
  }

  async function showOrders() {
    if (!user) { setAuthOpen(true); return; }
    try { if (!demoMode) setOrders(await request<Order[]>("/api/orders")); go("orders"); }
    catch (error) { message(error); }
  }

  function beginEdit(product?: Product) {
    setEditing(product ?? null);
    setProductForm(product
      ? { name: product.name, description: product.description, sku: product.sku, price: String(product.price), stockQuantity: String(product.stockQuantity), isActive: product.isActive }
      : { name: "", description: "", sku: "", price: "", stockQuantity: "", isActive: true });
  }

  async function saveProduct(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!user || user.role !== "Admin") return;
    setLoading(true);
    try {
      const body = { ...productForm, version: editing?.version, price: Number(productForm.price), stockQuantity: Number(productForm.stockQuantity) };
      if (editing) await request<Product>(`/api/products/${editing.id}`, { method: "PUT", body: JSON.stringify(body) });
      else await request<Product>("/api/products", { method: "POST", body: JSON.stringify(body) });
      await reloadProducts(); beginEdit(); setNotice("Product saved.");
    } catch (error) { message(error); }
    finally { setLoading(false); }
  }

  async function deleteProduct(product: Product) {
    if (!user || user.role !== "Admin" || !window.confirm(`Archive ${product.name}?`)) return;
    try { await request<void>(`/api/products/${product.id}`, { method: "DELETE" }); await reloadProducts(); setNotice("Product archived."); }
    catch (error) { message(error); }
  }

  return <div className="site-shell">
    <div className="announcement">MIRA storefront demo <span>·</span> Checkout does not collect payment</div>
    <header className="site-header"><div className="container-xl header-inner">
      <button className="icon-button mobile-menu" aria-label="Open menu" onClick={() => setMenuOpen(!menuOpen)}><Icon name="menu" /></button>
      <button className="wordmark" onClick={() => go("home")}>MIRA</button>
      <nav className={menuOpen ? "main-nav open" : "main-nav"} aria-label="Main navigation"><button onClick={() => go("shop")}>Shop</button><button onClick={() => { setSort("new"); go("shop"); }}>New arrivals</button><button onClick={() => { go("home"); window.setTimeout(() => document.getElementById("story")?.scrollIntoView({ behavior: "smooth" }), 100); }}>Our story</button>{user?.role === "Admin" && <button onClick={() => go("admin")}>Manage products</button>}</nav>
      <div className="header-actions"><button className="icon-button" aria-label="Search products" onClick={() => { go("shop"); window.setTimeout(() => document.getElementById("catalog-search")?.focus(), 100); }}><Icon name="search" /></button><button className="icon-button" aria-label="Account" onClick={() => user ? showOrders() : setAuthOpen(true)}><Icon name="user" /></button><button className="icon-button bag-button" aria-label={`Shopping bag with ${cartCount} items`} onClick={() => setCartOpen(true)}><Icon name="bag" /><span>{cartCount}</span></button></div>
    </div></header>

    {notice && <div className="notice" role="status"><span>{notice}</span><button aria-label="Dismiss message" onClick={() => setNotice("")}><Icon name="close" size={16} /></button></div>}
    {demoMode && <div className="demo-ribbon">Demo preview · sample products and simulated checkout</div>}

    {page === "home" && <>
      <section className="hero-section"><div className="hero-copy"><h1>Considered pieces<br />for everyday living</h1><p>Modern essentials, made to last. Timeless style for a more intentional wardrobe.</p><button className="primary-button" onClick={() => go("shop")}>Shop the collection <Icon name="arrow" size={18} /></button></div><div className="hero-image" role="img" aria-label="Model wearing neutral contemporary clothing" /></section>
      <div className="benefits"><div className="container-xl benefits-inner"><span>Explore the catalog</span><span>Create an account</span><span>Save items to your bag</span><span>Try simulated checkout</span></div></div>
      <section className="container-xl arrivals-section"><div className="section-title"><h2>New arrivals</h2><button className="text-link" onClick={() => go("shop")}>View all <Icon name="arrow" size={18} /></button></div><div className="row g-3 g-lg-4">{visibleProducts.slice(0, 4).map((product) => <div className="col-6 col-lg-3" key={product.id}><ProductCard product={product} onOpen={setSelected} onAdd={addToCart} /></div>)}</div>{!visibleProducts.length && <p className="empty-message">The collection is currently unavailable. Please try again shortly.</p>}</section>
      <section className="story-band" id="story"><div className="container-xl"><p className="story-small">THE MIRA EDIT</p><h2>A quieter kind of luxury</h2><p>Pieces to reach for today, and for years to come.</p><button className="outline-button" onClick={() => go("shop")}>Explore the collection <Icon name="arrow" size={18} /></button></div></section>
    </>}

    {page === "shop" && <main className="container-xl catalog-page"><div className="page-heading"><span>THE COLLECTION</span><h1>Everyday, considered.</h1><p>Discover versatile pieces chosen for the way you live.</p></div><div className="catalog-toolbar"><label className="search-field"><Icon name="search" size={19} /><input id="catalog-search" type="search" placeholder="Search by name, description or SKU" value={search} maxLength={100} onChange={(event) => { setSearch(event.target.value); setCatalogPage(1); }} /></label><label className="sort-field">Sort by <select value={sort} onChange={(event) => { setSort(event.target.value); setCatalogPage(1); }}><option value="featured">Featured</option><option value="new">Newest</option><option value="low">Price: low to high</option><option value="high">Price: high to low</option></select></label></div><p className="result-count">{visibleProducts.length} {visibleProducts.length === 1 ? "piece" : "pieces"}</p><div className="row g-3 g-lg-4">{visibleProducts.map((product) => <div className="col-6 col-lg-3" key={product.id}><ProductCard product={product} onOpen={setSelected} onAdd={addToCart} /></div>)}</div>{!visibleProducts.length && <p className="empty-message">No products found. Try another search.</p>}{!demoMode && totalProducts > 24 && <nav className="catalog-pagination" aria-label="Catalog pages"><button disabled={catalogPage === 1} onClick={() => setCatalogPage(p => p - 1)}>Previous</button><span>Page {catalogPage} of {Math.ceil(totalProducts / 24)}</span><button disabled={catalogPage * 24 >= totalProducts} onClick={() => setCatalogPage(p => p + 1)}>Next</button></nav>}</main>}

    {page === "orders" && <main className="container-xl account-page"><div className="account-heading"><div><span>YOUR ACCOUNT</span><h1>Orders</h1><p>{user ? `Signed in as ${user.email}` : "Sign in to view your orders."}</p></div><button className="text-link" onClick={signOut}>Sign out <Icon name="arrow" size={17} /></button></div>{orders.length ? orders.map((order) => <article className="order-card" key={order.id}><div><strong>Order {order.id.slice(0, 8).toUpperCase()}</strong><span>{new Date(order.createdAtUtc).toLocaleDateString()}</span></div><div><span>{order.status}</span><strong>{currency(order.totalAmount)}</strong></div><ul>{order.items.map((item) => <li key={item.id}>{item.productName} × {item.quantity}</li>)}</ul></article>) : <p className="empty-message">You have no orders yet.</p>}</main>}

    {page === "admin" && user?.role === "Admin" && <main className="container-xl admin-page"><div className="page-heading"><span>PRODUCT MANAGEMENT</span><h1>Catalog</h1><p>Add and edit products served by your API.</p></div><div className="admin-layout"><div className="admin-list"><h2>Products</h2>{products.map((product) => <div className="admin-row" key={product.id}><div><strong>{product.name}</strong><small>{product.sku} · {currency(product.price)} · {product.stockQuantity} in stock</small></div><button onClick={() => beginEdit(product)}>Edit</button><button aria-label={`Archive ${product.name}`} onClick={() => deleteProduct(product)}><Icon name="trash" size={18} /></button></div>)}</div><form className="admin-form" onSubmit={saveProduct}><div className="form-header"><h2>{editing ? "Edit product" : "New product"}</h2>{editing && <button type="button" onClick={() => beginEdit()}>Cancel</button>}</div><label>Name<input required maxLength={200} value={productForm.name} onChange={(event) => setProductForm({ ...productForm, name: event.target.value })} /></label><label>Description<textarea required maxLength={2000} rows={3} value={productForm.description} onChange={(event) => setProductForm({ ...productForm, description: event.target.value })} /></label><label>SKU<input required maxLength={100} value={productForm.sku} onChange={(event) => setProductForm({ ...productForm, sku: event.target.value })} /></label><div className="form-pair"><label>Price<input type="number" required min="0.01" step="0.01" value={productForm.price} onChange={(event) => setProductForm({ ...productForm, price: event.target.value })} /></label><label>Stock<input type="number" required min="0" step="1" value={productForm.stockQuantity} onChange={(event) => setProductForm({ ...productForm, stockQuantity: event.target.value })} /></label></div>{editing && <label className="checkbox-line"><input type="checkbox" checked={productForm.isActive} onChange={(event) => setProductForm({ ...productForm, isActive: event.target.checked })} /> Active</label>}<button className="primary-button" disabled={loading} type="submit">Save product</button><p>To assign an image, add the SKU to <code>src/catalog.ts</code>.</p></form></div></main>}

    <footer className="site-footer"><div className="container-xl footer-inner"><div><span className="footer-wordmark">MIRA</span><p>Considered pieces for everyday living.</p></div><nav aria-label="Footer navigation"><button onClick={() => go("shop")}>Shop</button><button onClick={() => showOrders()}>Orders</button><button onClick={() => setAuthOpen(true)}>Account</button></nav><small>© {new Date().getFullYear()} MIRA. Template preview.</small></div></footer>

    {selected && <div className="overlay" onMouseDown={() => setSelected(null)}><section className="product-dialog" role="dialog" aria-modal="true" aria-label={selected.name} onMouseDown={(event) => event.stopPropagation()}><button className="dialog-close" aria-label="Close product" onClick={() => setSelected(null)}><Icon name="close" /></button><div className="dialog-image"><ProductVisual product={selected} /></div><div className="dialog-content"><span>THE COLLECTION</span><h2>{selected.name}</h2><p className="dialog-price">{currency(selected.price)}</p><p>{selected.description}</p><p className="sku-line">SKU {selected.sku} · {selected.stockQuantity} available</p><button className="primary-button" disabled={selected.stockQuantity < 1 || loading} onClick={() => addToCart(selected)}>{selected.stockQuantity ? "Add to bag" : "Sold out"} <Icon name="arrow" size={18} /></button></div></section></div>}

    {cartOpen && <div className="overlay drawer-overlay" onMouseDown={() => setCartOpen(false)}><aside className="cart-drawer" role="dialog" aria-modal="true" aria-label="Shopping bag" onMouseDown={(event) => event.stopPropagation()}><div className="drawer-heading"><div><span>YOUR BAG</span><h2>Shopping bag ({cartCount})</h2></div><button aria-label="Close bag" onClick={() => setCartOpen(false)}><Icon name="close" /></button></div><div className="drawer-items">{cart.items.length ? cart.items.map((item) => <article className="cart-item" key={item.id}><div className="cart-item-image">{catalogImageBySku[item.productSku] && <img src={catalogImageBySku[item.productSku]} alt="" />}</div><div className="cart-item-copy"><h3>{item.productName}</h3><p>{currency(item.unitPrice)}</p>{item.isAvailable === false && <small>Unavailable — remove this item.</small>}{item.availableStock !== undefined && item.quantity > item.availableStock && <small>Only {item.availableStock} available.</small>}<div className="quantity"><button aria-label={`Decrease ${item.productName}`} disabled={item.quantity <= 1} onClick={() => changeQuantity(item.id, item.quantity - 1)}><Icon name="minus" size={16} /></button><span>{item.quantity}</span><button aria-label={`Increase ${item.productName}`} onClick={() => changeQuantity(item.id, item.quantity + 1)}><Icon name="plus" size={16} /></button></div></div><button className="remove-button" aria-label={`Remove ${item.productName}`} onClick={() => removeItem(item.id)}><Icon name="trash" size={18} /></button></article>) : <p className="empty-message">Your bag is empty.</p>}</div>{cart.items.length > 0 && <div className="drawer-footer"><div><span>Subtotal</span><strong>{currency(cart.total)}</strong></div><p>Shipping and taxes are not calculated by this API. This creates a simulated Pending order; no payment is collected.</p><button className="primary-button" disabled={loading || cart.items.some(item => item.isAvailable === false || (item.availableStock !== undefined && item.quantity > item.availableStock))} onClick={checkout}>Create simulated order <Icon name="arrow" size={18} /></button></div>}</aside></div>}

    {authOpen && <div className="overlay" onMouseDown={() => setAuthOpen(false)}><section className="auth-dialog" role="dialog" aria-modal="true" aria-label="Account" onMouseDown={(event) => event.stopPropagation()}><button className="dialog-close" aria-label="Close account" onClick={() => setAuthOpen(false)}><Icon name="close" /></button><span>YOUR ACCOUNT</span><h2>{authMode === "login" ? "Welcome back" : "Create an account"}</h2><p>{authMode === "login" ? "Sign in to save your bag and view orders." : "Join us to keep track of your orders."}</p><form onSubmit={submitAuth}>{authMode === "register" && <div className="form-pair"><label>First name<input name="firstName" required autoComplete="given-name" /></label><label>Last name<input name="lastName" required autoComplete="family-name" /></label></div>}<label>Email<input name="email" type="email" required autoComplete="email" /></label><label>Password<input name="password" type="password" required minLength={authMode === "register" ? 12 : 1} autoComplete={authMode === "login" ? "current-password" : "new-password"} /></label><button className="primary-button" type="submit" disabled={loading}>{authMode === "login" ? "Sign in" : "Create account"} <Icon name="arrow" size={18} /></button></form><button className="auth-switch" onClick={() => setAuthMode(authMode === "login" ? "register" : "login")}>{authMode === "login" ? "New here? Create an account" : "Already have an account? Sign in"}</button></section></div>}
  </div>;
}

export default App;
