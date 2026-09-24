export type Product = {
  id: string;
  name: string;
  description: string;
  sku: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
  version?: number;
  createdAtUtc?: string;
};

export type User = {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
};

export type CartItem = {
  id: string;
  productId: string;
  productName: string;
  productSku: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  isAvailable?: boolean;
  availableStock?: number;
};

export type Cart = { items: CartItem[]; total: number };

export type Order = {
  id: string;
  totalAmount: number;
  status: string;
  createdAtUtc: string;
  items: CartItem[];
};

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

export async function request<T>(path: string, options: RequestInit = {}, onHeaders?: (headers: Headers) => void): Promise<T> {
  let csrfToken = "";
  if (!["GET", "HEAD", "OPTIONS"].includes(options.method ?? "GET")) {
    const csrf = await fetch(`${baseUrl}/api/auth/session/csrf`, { credentials: "same-origin", cache: "no-store" });
    if (!csrf.ok) throw new Error("Session could not be verified. Refresh and try again.");
    csrfToken = ((await csrf.json()) as { token: string }).token;
  }
  const response = await fetch(`${baseUrl}${path}`, {
    ...options,
    credentials: "same-origin",
    headers: {
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(csrfToken ? { "X-CSRF-TOKEN": csrfToken } : {}),
      ...options.headers,
    },
  });

  if (!response.ok) {
    if (response.status === 401 && !path.startsWith("/api/auth")) {
      window.dispatchEvent(new Event("ecommerce:session-expired"));
    }
    let message = `Request failed (${response.status}).`;
    try {
      const body = (await response.json()) as { message?: string; title?: string };
      message = body.message || body.title || message;
    } catch {
      // The API can return an empty body for some failures.
    }
    throw new Error(message);
  }

  onHeaders?.(response.headers);

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}
