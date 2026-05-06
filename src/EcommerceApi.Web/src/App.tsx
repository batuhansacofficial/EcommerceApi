import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import "./App.css";

type Product = {
  id: string;
  name: string;
  description: string;
  sku: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
};

type LoginResponse = {
  accessToken: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
};

type CartItem = {
  id: string;
  productId: string;
  productName: string;
  productSku: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
};

type CartResponse = {
  items: CartItem[];
  total: number;
};

const apiBaseUrl = "http://localhost:5211";

function App() {
  const [products, setProducts] = useState<Product[]>([]);
  const [search, setSearch] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string>("");

  const [email, setEmail] = useState<string>("customer@example.com");
  const [password, setPassword] = useState<string>("Password123!");
  const [currentUser, setCurrentUser] = useState<LoginResponse | null>(null);
  const [loginError, setLoginError] = useState<string>("");

  const [cart, setCart] = useState<CartResponse | null>(null);
  const [cartMessage, setCartMessage] = useState<string>("");

  async function loadProducts() {
      try {
          const queryString = search.trim()
              ? `?search=${encodeURIComponent(search.trim())}`
              : "";

          const response = await fetch(`${apiBaseUrl}/api/products${queryString}`);

          if (!response.ok) {
              throw new Error(`API request failed with status ${response.status}`);
          }

          const data = (await response.json()) as Product[];
          setProducts(data);
          setErrorMessage("");
      } catch (error) {
          const message =
              error instanceof Error ? error.message : "Unknown error occurred.";

          setErrorMessage(message);
      }
  }

  useEffect(() => {
      loadProducts();
  }, [search]);

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      setLoginError("");

      const response = await fetch(`${apiBaseUrl}/api/auth/login`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          email,
          password,
        }),
      });

      if (!response.ok) {
        throw new Error("Invalid email or password.");
      }

      const data = (await response.json()) as LoginResponse;

      localStorage.setItem("accessToken", data.accessToken);
      setCurrentUser(data);

      await loadCart(data.accessToken);
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Login failed.";

      setLoginError(message);
    }
  }

  function handleLogout() {
    localStorage.removeItem("accessToken");
    setCurrentUser(null);
    setCart(null);
    setCartMessage("");
  }

  async function loadCart(accessToken: string) {
    const response = await fetch(`${apiBaseUrl}/api/cart`, {
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Cart request failed with status ${response.status}`);
    }

    const data = (await response.json()) as CartResponse;
    setCart(data);
  }

  async function addToCart(productId: string) {
    const accessToken = localStorage.getItem("accessToken");

    if (!accessToken) {
      setCartMessage("Login before adding products to the cart.");
      return;
    }

    try {
      setCartMessage("");

      const response = await fetch(`${apiBaseUrl}/api/cart/items`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${accessToken}`,
        },
        body: JSON.stringify({
          productId,
          quantity: 1,
        }),
      });

      if (!response.ok) {
        throw new Error(`Add to cart failed with status ${response.status}`);
      }

      const data = (await response.json()) as CartResponse;
      setCart(data);
      setCartMessage("Product added to cart.");
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Add to cart failed.";

      setCartMessage(message);
    }
  }

  async function checkout() {
    const accessToken = localStorage.getItem("accessToken");

    if (!accessToken) {
      setCartMessage("Login before checkout.");
      return;
    }

    try {
      setCartMessage("");

      const response = await fetch(`${apiBaseUrl}/api/checkout`, {
        method: "POST",
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      });

      if (!response.ok) {
        throw new Error(`Checkout failed with status ${response.status}`);
      }

      await response.json();

      setCart({
        items: [],
        total: 0,
      });

      await loadProducts();

      setCartMessage("Checkout completed.");
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Checkout failed.";

      setCartMessage(message);
    }
  }

  return (
    <main className="page">
      <section className="hero">
        <h1>E-Commerce Store</h1>
        <p>Products loaded from the ASP.NET Core API.</p>
      </section>

      <section className="auth-panel">
        {currentUser ? (
          <div>
            <p>
              Signed in as <strong>{currentUser.email}</strong> ({currentUser.role})
            </p>
            <button type="button" onClick={handleLogout}>
              Logout
            </button>
          </div>
        ) : (
          <form onSubmit={handleLogin}>
            <h2>Login</h2>

            <label htmlFor="email">Email</label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />

            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />

            {loginError && <p className="form-error">{loginError}</p>}

            <button type="submit">Login</button>
          </form>
        )}
      </section>

      {currentUser && (
        <section className="cart-panel">
          <h2>Cart</h2>

          {cart && cart.items.length > 0 ? (
            <ul className="cart-list">
              {cart.items.map((item) => (
                <li key={item.id}>
                  <span>
                    {item.productName} × {item.quantity}
                  </span>
                  <strong>${item.lineTotal.toFixed(2)}</strong>
                </li>
              ))}
            </ul>
          ) : (
            <p>Your cart is empty.</p>
          )}

          <p>
            Total: <strong>${(cart?.total ?? 0).toFixed(2)}</strong>
          </p>

          {cart && cart.items.length > 0 && (
            <button type="button" onClick={checkout}>
                Checkout
            </button>
          )}

          {cartMessage && <p>{cartMessage}</p>}
        </section>
      )}

      <section className="toolbar">
        <label htmlFor="product-search">Search products</label>
        <input
          id="product-search"
          type="search"
          placeholder="Search by name, description, or SKU"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
      </section>

      {errorMessage && (
        <section className="error">
          <strong>Failed to load products.</strong>
          <p>{errorMessage}</p>
        </section>
      )}

      <section className="product-grid">
        {products.map((product) => (
          <article className="product-card" key={product.id}>
            <h2>{product.name}</h2>
            <p>{product.description}</p>

            <dl>
              <div>
                <dt>SKU</dt>
                <dd>{product.sku}</dd>
              </div>

              <div>
                <dt>Price</dt>
                <dd>${product.price.toFixed(2)}</dd>
              </div>

              <div>
                <dt>Stock</dt>
                <dd>{product.stockQuantity}</dd>
              </div>
            </dl>

            <button
              type="button"
              disabled={!currentUser || product.stockQuantity <= 0}
              onClick={() => addToCart(product.id)}
            >
              Add to Cart
            </button>
          </article>
        ))}
      </section>

      {!errorMessage && products.length === 0 && (
        <p className="empty-state">No products found.</p>
      )}
    </main>
  );
}

export default App;
