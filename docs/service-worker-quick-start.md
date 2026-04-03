# Quick Start: Testing Service Worker Notifications

---

## Local PWA Testing

Service workers require either **`localhost`** or an **HTTPS** origin. `ng serve` (webpack dev server) does **not** serve `ngsw-worker.js` correctly — always use `ng build` followed by a static file server.

### Prerequisites (install once)

```powershell
# Static file server
npm install -g http-server

# HTTPS tunnel for Android testing (optional)
npm install -g ngrok
```

### Step 1 — Build

```powershell
cd client

# Development build (SW enabled in all environments)
npx ng build

# Production build
npx ng build --configuration production
```

The output lands in `dist/Divvy/browser/`. Verify the SW files are present:

```powershell
Get-ChildItem dist/Divvy/browser | Where-Object { $_.Name -match 'ngsw|custom-sw|manifest' }
# Expected: custom-sw.js  ngsw-worker.js  ngsw.json  manifest.webmanifest
```

### Step 2 — Serve locally

```powershell
# -c-1 disables caching so you always get fresh files during development
http-server dist/Divvy/browser -p 8080 -c-1
```

Open **`http://localhost:8080`** in Chrome.

### Step 3 — Verify the service worker in DevTools

1. Open **DevTools → Application → Service Workers**.
2. Confirm `custom-sw.js` shows **Source**, **Status: activated and running**.
3. Check **Application → Manifest** — all 4 PNG icons (192 any, 512 any, 192 maskable, 512 maskable) should preview correctly.
4. The "Install" banner (or ⊕ in the address bar) confirms Chrome considers the app installable.

### Step 4 — Force an update during development

The SW caches aggressively. When you rebuild, click **Update** in
`DevTools → Application → Service Workers`, or tick **Update on reload** while
developing.

```javascript
// Or run this in the DevTools console to unregister and start clean:
navigator.serviceWorker.getRegistrations().then(regs => regs.forEach(r => r.unregister()));
```

### Step 5 — Install on Android (Chrome) — Add to Home Screen

Android requires an **HTTPS** URL. Use ngrok to expose the local server:

```powershell
# Terminal 1 — serve
http-server dist/Divvy/browser -p 8080 -c-1

# Terminal 2 — tunnel (creates a public HTTPS URL)
ngrok http 8080
```

1. Copy the `https://xxxx.ngrok.io` URL from the ngrok output.
2. Open that URL in **Chrome on Android**.
3. After ~30 seconds Chrome shows an **"Add Divvy to Home Screen"** banner at the bottom of the screen. Tap it, then tap **Add**.
   - Alternatively: tap the **⋮ menu → Add to Home screen**.
4. Launch the app from the home screen — it opens in **standalone mode** (no browser chrome), confirming `"display": "standalone"` in the manifest is working.
5. The home screen icon will be the 192 × 192 PNG you see in `assets/icons/icon-192x192.png`.

> **Tip:** if the banner never appears, open `chrome://flags/#bypass-app-banner-engagement-checks` on the device and enable it to bypass Chrome's repeated-visit threshold during testing.

### Step 6 — Lighthouse PWA audit

1. In Chrome DevTools, open the **Lighthouse** tab.
2. Select **Progressive Web App** (deselect others for speed).
3. Click **Analyze page load**.
4. All PWA checks should be green. Key checks: installability criteria, service worker registered, HTTPS, icons, theme-color.

---

## Testing Push Notifications

Use the dev-only push test panel at `/dev/push-test` (requires login). This panel lets you send a test push to yourself for any notification type.

1. Log in and navigate to `http://localhost:8080/dev/push-test`.
2. Select a notification type from the dropdown.
3. Click **Send Test Push** — the browser should display a notification.
4. Click the notification to confirm the deep-link navigation works.

> **Production reminder:** `/dev/push-test` is gated by `DevOnlyGuard` and is unreachable in a production build.

---

## Viewing Service Worker Logs

**In DevTools:**
1. Application > Service Workers
2. Click "custom-sw.js" to view source
3. Console logs appear in main DevTools console

**Service Worker Lifecycle:**
```
Installing → Installed → Activating → Activated
```

## Common Issues

### "Service Worker registration failed"
- ✅ Use `http-server` (not `ng serve`)
- ✅ Check file path: `custom-sw.js` should be at root of the served directory
- ✅ Clear browser cache and reload

### "Notification permission denied"
- ✅ Click lock icon in address bar
- ✅ Notifications > Allow
- ✅ Refresh page

### Notification doesn't appear
- ✅ Check service worker is "activated" in DevTools
- ✅ Verify system notifications are enabled at the OS level
- ✅ Run the push test from `/dev/push-test` to confirm the SW handler works

## Quick Debug Commands

**In Browser Console:**
```javascript
// Check if service worker is registered
navigator.serviceWorker.getRegistration().then(reg => console.log(reg));

// Check notification permission
console.log(Notification.permission);

// Unregister service worker (for clean testing)
navigator.serviceWorker.getRegistration().then(reg => reg.unregister());
```
