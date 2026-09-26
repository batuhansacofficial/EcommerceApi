# Storefront assets

Images are stored in `src/EcommerceApi.Web/public/images` and served from `/images/`.

| File | Use |
|---|---|
| `hero.png` | Home page hero |
| `story.png` | Home page collection section |
| `overshirt.png` | Overshirt product |
| `trousers.png` | Trouser product |
| `knit.png` | Knitwear product |
| `tote.png` | Tote product |

The bundled images are generated sample assets for the demo catalog. They do not document actual products offered for sale. Replace them with your own licensed product images when adapting the storefront.

## Replace an image

1. Add the replacement file to `src/EcommerceApi.Web/public/images`.
2. Update its SKU mapping in `src/EcommerceApi.Web/src/catalog.ts`. Home page image references are in `src/EcommerceApi.Web/src/App.tsx`.
3. Update any relevant alternative text and rebuild the storefront.

Production image policy allows same-origin assets and data URLs. External image hosting requires an explicit CSP configuration change.

## Store configuration

Currency formatting currently uses `VITE_CURRENCY`, with USD as the default. It must be set when building the frontend; setting a runtime variable on the deployed container does not rebuild the static bundle. Changing the displayed currency does not convert prices. See [known limitations](KNOWN-LIMITATIONS.md) before changing the currency of a populated store.
