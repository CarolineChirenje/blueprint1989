# Batanai â€” Brand Identity

## Overview

This document defines the Batanai visual identity system: the meaning behind the brand, the icon and logo design, the colour palette, typography, and how these elements are applied across the Progressive Web App.

---

## Brand Name

**Batanai** is a Shona word meaning **"togetherness"** or **"unity."** It captures the platform's core purpose â€” bringing people together to manage shared finances transparently.

The tagline **"Bambanani"** (Zulu/Ndebele for "hold each other" / "support one another") reinforces the communal philosophy. Together, the name and tagline communicate trust, mutual accountability, and collective financial well-being.

---

## Brand Mark â€” "Unity Savings"

The Batanai icon is a circular emblem that encodes three ideas in a single mark:

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                                         â”‚
â”‚         â—  (community dot)              â”‚
â”‚                                         â”‚
â”‚    â—   â•­â”â”â”â”â”â”â”â”â”â”â”â”â”â”â•®   â—            â”‚
â”‚        â”‚  â•­â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â•® â”‚                â”‚
â”‚        â”‚  â”‚  ðŸ· + ðŸª™  â”‚ â”‚                â”‚
â”‚        â”‚  â•°â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â•¯ â”‚                â”‚
â”‚    â—   â•°â”â”â”â”â”â”â”â”â”â”â”â”â”â”â•¯   â—            â”‚
â”‚                                         â”‚
â”‚         â—  (community dot)              â”‚
â”‚                                         â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

### 1. Unity Ring

A bold green ring (`#237A49`) forms the dominant shape. It represents:

- **Togetherness** â€” a circle with no beginning or end, symbolising ongoing mutual support.
- **The group** â€” every Batanai feature operates within the context of a group; the ring is the group boundary.
- **Trust** â€” a closed, protective shape encircling the members' shared funds.

### 2. Community Dots

Four small green circles are placed on the ring at 12, 3, 6, and 9 o'clock. They represent group members â€” the people who make Batanai work. Four dots were chosen for visual balance; they do not imply a group-size limit.

At small icon sizes (32px and below), the dots merge naturally into the ring stroke, preserving a clean silhouette without losing the community metaphor at larger sizes.

### 3. Piggy Bank with Gold Coin

A flat-design pink piggy bank sits at the centre of the ring. A gold coin drops into its coin slot with a subtle motion trail above it. This element communicates:

- **Savings** â€” the primary purpose of both Majana (expense splitting) and Mukando (rotating savings) cycles.
- **Action** â€” the coin mid-drop conveys active participation rather than a static symbol.
- **Approachability** â€” the piggy bank is universally recognised and non-intimidating, intentionally avoiding currency symbols that would tie the brand to a single economy.

A green accent arc underneath the pig echoes the ring's colour, grounding the pig inside the circle and reinforcing brand-colour consistency.

---

## Logo Variants

Two SVG source files exist. All raster assets (PNG, ICO) are machine-generated from the first.

| Variant | File | Canvas | Usage |
|---|---|---|---|
| **Emblem** (icon) | `assets/icons/icon.svg` | 512 Ã— 512 | App icon, favicon, manifest, splash screen, about page, Apple touch icon |
| **Full logo** (emblem + wordmark) | `assets/icons/logo-full.svg` | 512 Ã— 640 | Login screen |

### Why Two Variants?

- **Emblem only** is used wherever icons appear at small sizes (16â€“512px). Text becomes illegible below ~80px, and mobile home screens already display the app name underneath the icon â€” duplicating it inside the icon is redundant.
- **Full logo** is used only on the login screen (~165px render size), where it is the first brand touchpoint for new users. The wordmark "BATANAI" in Inter 800 weight provides immediate brand-name recognition before the user has seen the app name elsewhere.

---

## Colour Palette

### Primary Colours

| Swatch | Name | Hex | Role |
|---|---|---|---|
| ðŸŸ© | **Brand Green** | `#237A49` | Unity ring, community dots, wordmark, all UI primary controls (buttons, toggles, tabs, progress bars) |
| ðŸ©· | **Piggy Pink** | `#EF8FA1` | Piggy bank body â€” secondary accent used only in the icon, not in the UI |
| ðŸŸ¡ | **Coin Gold** | `#FFD700` | Dropping coin â€” tertiary accent used only in the icon |
| â¬œ | **Clean White** | `#FFFFFF` | Icon inner circle, pig outline strokes, app card backgrounds |

### Extended UI Palette (CSS custom properties)

These are defined in `styles.css` under `:root` and applied throughout the Angular Material theme:

| Variable | Hex | Usage |
|---|---|---|
| `--primary` | `#237A49` | All interactive elements, links, filled buttons |
| `--primary-hover` | `#1D643C` | Hover state on primary elements |
| `--primary-dark` | `#1A5735` | Active/pressed state |
| `--primary-light` | `#E9F3EC` | Subtle backgrounds (selected rows, badges) |
| `--primary-subtle` | `#F0F7F2` | Card hover, icon wraps |
| `--text-on-primary` | `#FFFFFF` | Text/icons on green backgrounds |

### Colour Consistency Rule

The icon's green (`#237A49`) is **the same value** used in every CSS variable, Material Design override, meta theme-color tag, and manifest background_color. A single hex value flows from brand mark to UI, ensuring visual unity across all touchpoints.

---

## Typography

| Context | Family | Weight | Notes |
|---|---|---|---|
| **App UI** | Inter | 400 / 500 / 600 / 700 | Loaded from Google Fonts; used for all body text, headings, and form labels |
| **Logo wordmark** | Inter | 800 (ExtraBold) | "BATANAI" text in `logo-full.svg`, letter-spacing 6px |
| **Icon internals** | N/A | â€” | No text inside the emblem icon; all communication is purely graphical |
| **Material Icons** | Material Icons | â€” | Google's icon font for UI action icons (not branding) |

### Why Inter?

Inter was designed for screen readability at small sizes with tall x-height and open apertures. It renders crisply on both high-DPI and low-DPI mobile screens â€” critical for a mobile-first PWA.

---

## Icon Generation Pipeline

All raster icons are generated from `icon.svg` using a Node.js script (`scripts/generate-icons.mjs`). No icons are hand-crafted as PNGs.

### Pipeline

```
icon.svg (source of truth)
    â”‚
    â”œâ”€â”€â–º icon-512x512.png          (regular, rx="72" rounded corners)
    â”œâ”€â”€â–º icon-192x192.png          (regular, rx="72" rounded corners)
    â”œâ”€â”€â–º icon-maskable-512x512.png (maskable, rx="0" square edges)
    â”œâ”€â”€â–º icon-maskable-192x192.png (maskable, rx="0" square edges)
    â””â”€â”€â–º favicon.ico               (bundled 16 + 32 + 48 px)
```

### How It Works

1. The script reads `icon.svg` and generates **regular** PNGs via `sharp` at 512px and 192px.
2. For **maskable** variants, it replaces `rx="72"` with `rx="0"` on the background `<rect id="icon-bg">`, removing rounded corners so the OS can apply its own mask shape (circle on Android, squircle on iOS, rounded square on Windows).
3. For `favicon.ico`, it rasterizes at 16, 32, and 48px, then bundles them into a single ICO file using `png-to-ico`.

### Running the Pipeline

```bash
# From workspace root
node scripts/generate-icons.mjs
```

**Dependencies** (installed in `scripts/`): `sharp`, `png-to-ico`.

### Convention: `id="icon-bg"`

The SVG must contain a `<rect id="icon-bg" width="512" height="512" rx="72">` element. The generation script locates this element by its `id` and `rx` attribute to produce the maskable variant. If this convention is broken, maskable icons will not generate correctly.

---

## Asset Inventory

| File | Type | Size | Purpose |
|---|---|---|---|
| `client/src/assets/icons/icon.svg` | SVG | 512 Ã— 512 | Source emblem â€” all PNGs and ICO are derived from this |
| `client/src/assets/icons/logo-full.svg` | SVG | 512 Ã— 640 | Emblem + "BATANAI" wordmark for login screen |
| `client/src/assets/icons/icon-512x512.png` | PNG | 512 Ã— 512 | PWA manifest icon (`purpose: any`) |
| `client/src/assets/icons/icon-192x192.png` | PNG | 192 Ã— 192 | PWA manifest icon (`purpose: any`), Apple touch icon |
| `client/src/assets/icons/icon-maskable-512x512.png` | PNG | 512 Ã— 512 | PWA manifest icon (`purpose: maskable`) |
| `client/src/assets/icons/icon-maskable-192x192.png` | PNG | 192 Ã— 192 | PWA manifest icon (`purpose: maskable`) |
| `client/src/favicon.ico` | ICO | 16+32+48 | Browser tab favicon |

---

## Where Brand Elements Appear

| Location | File | Element |
|---|---|---|
| **Browser tab** | `index.html` | `<link rel="icon" href="assets/icons/icon.svg">` + `favicon.ico` |
| **PWA home screen** | `manifest.webmanifest` | `icons` array (all 5 entries) |
| **PWA splash screen** | `index.html` | `<img src="assets/icons/icon.svg">` on `#237A49` background |
| **Apple touch icon** | `index.html` | `<link rel="apple-touch-icon" href="assets/icons/icon-192x192.png">` |
| **Login screen** | `login.component.html` | `<img src="assets/icons/logo-full.svg">` at ~165 Ã— 165px |
| **About screen** | `about.component.html` | `<img src="assets/icons/icon.svg">` at 22 Ã— 22px |
| **Theme colour** | `index.html` + `manifest.webmanifest` | `<meta name="theme-color" content="#237A49">` |

---

## Design Rationale at Scale

The emblem was designed to degrade gracefully across all sizes:

| Size | What's Visible | Context |
|---|---|---|
| **512px** | Full detail â€” ring, dots, pig body, ear, snout, nostrils, eye, legs, tail, coin slot, gold coin with trail, green accent arc | PWA install prompt, store listing |
| **192px** | Ring, dots, pig shape, coin clearly readable | Home screen icon, Apple touch icon |
| **48px** | Green ring, pink pig silhouette, gold dot | Favicon (high-DPI) |
| **32px** | Green circle with pink centre | Favicon (standard) |
| **16px** | Green circle with pink centre | Favicon (smallest), tab bar |

The strong circular silhouette ensures the icon remains **recognisable and distinctive** even at the smallest sizes where detail is lost.

---

## Accessibility

Both SVG files include:

- `role="img"` for assistive technology identification
- `<title>` for a short accessible name ("Batanai")
- `<desc>` for a longer description of the visual content
- `aria-labelledby` linking both elements

These ensure screen readers announce meaningful content when the icon is encountered outside of an `<img>` tag with `alt` text.

---

## Updating the Brand

To modify the brand mark:

1. Edit `client/src/assets/icons/icon.svg` (the single source of truth).
2. Ensure the `<rect id="icon-bg" width="512" height="512" rx="72">` element is preserved.
3. If the full logo needs updating, edit `client/src/assets/icons/logo-full.svg` to match.
4. Run `node scripts/generate-icons.mjs` from the workspace root to regenerate all raster assets.
5. Verify the maskable variant has no rounded corners.
6. If the primary colour changes, update `styles.css` `:root` variables, `manifest.webmanifest` background/theme colours, and the `index.html` `<meta name="theme-color">` tag.
