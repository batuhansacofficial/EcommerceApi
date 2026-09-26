import type { Product } from "./api";

// Replace these SKU mappings with your own licensed product images.
export const catalogImageBySku: Record<string, string> = {
  "MIRA-OVERSHIRT": "/images/overshirt.png",
  "MIRA-TROUSERS": "/images/trousers.png",
  "MIRA-KNIT": "/images/knit.png",
  "MIRA-TOTE": "/images/tote.png",
};

export const demoProducts: Product[] = [
  { id: "demo-1", name: "The Relaxed Overshirt", description: "A softly structured layer in washed olive cotton.", sku: "MIRA-OVERSHIRT", price: 120, stockQuantity: 12, isActive: true },
  { id: "demo-2", name: "The Tailored Trouser", description: "An easy wide leg silhouette in ivory linen.", sku: "MIRA-TROUSERS", price: 140, stockQuantity: 9, isActive: true },
  { id: "demo-3", name: "The Cashmere Knit", description: "A timeless knit with a relaxed drape.", sku: "MIRA-KNIT", price: 180, stockQuantity: 7, isActive: true },
  { id: "demo-4", name: "The Everyday Tote", description: "A spacious carryall in deep olive leather.", sku: "MIRA-TOTE", price: 220, stockQuantity: 5, isActive: true },
];

export const currency = (value: number) =>
  new Intl.NumberFormat(undefined, { style: "currency", currency: import.meta.env.VITE_CURRENCY || "USD", maximumFractionDigits: 2 }).format(value);
