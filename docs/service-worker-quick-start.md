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

The output lands in `dist/Vitara/browser/`. Verify the SW files are present:

```powershell
Get-ChildItem dist/Vitara/browser | Where-Object { $_.Name -match 'ngsw|custom-sw|manifest' }
# Expected: custom-sw.js  ngsw-worker.js  ngsw.json  manifest.webmanifest
```

### Step 2 — Serve locally

```powershell
# -c-1 disables caching so you always get fresh files during development
http-server dist/Vitara/browser -p 8080 -c-1
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
http-server dist/Vitara/browser -p 8080 -c-1

# Terminal 2 — tunnel (creates a public HTTPS URL)
ngrok http 8080
```

1. Copy the `https://xxxx.ngrok.io` URL from the ngrok output.
2. Open that URL in **Chrome on Android**.
3. After ~30 seconds Chrome shows an **"Add Vitara to Home Screen"** banner at the bottom of the screen. Tap it, then tap **Add**.
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

## For Fast SW Notification Testing (2-minute timer instead of 2 hours)

### Step 1: Modify Timer Duration

**File:** `client/src/app/admin/bgl-reading.component.ts`

Find `start2HourTimer()` method (around line 225) and change:

```typescript
start2HourTimer() {
  this.timerActive = true;
  this.remainingSeconds = 120; // CHANGED: 2 minutes for testing (was 2 * 60 * 60)
  
  // Calculate when notification should fire
  const scheduledTime = Date.now() + (this.remainingSeconds * 1000);
  // ... rest stays the same
}
```

### Step 2: Modify Service Worker Check Interval

**File:** `client/src/custom-sw.js`

Change line 5:

```javascript
const NOTIFICATION_CHECK_INTERVAL = 10000; // CHANGED: Check every 10 seconds (was 60000)
```

### Step 3: Build and Serve

```powershell
cd client
npx ng build
http-server dist/Vitara/browser -p 4200 -c-1
```

### Step 4: Test Flow

1. Open `http://localhost:4200` in Chrome
2. Login as admin
3. Navigate to "BGL Assessment" > "Assess BGL"
4. Enter reading: `15.5` (triggers hyperglycemia)
5. Enter ketones: `0.4` (triggers 2-hour monitoring)
6. See instructions and timer (now 2 minutes)
7. **Open DevTools:**
   - Application > Service Workers (should see "activated and running")
   - Application > IndexedDB > VitaraNotifications (notification scheduled)
   - Application > Local Storage > bgl-ketone-timer (backup state)
8. **Wait 2 minutes** (or close tab and wait)
9. Notification should appear!

### Step 5: Test Persistence

**With timer running:**
1. Refresh page - timer should restore
2. Close tab and reopen - timer continues
3. Close browser entirely - notification still fires

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
- ✅ Use `http-server` or Angular's build server
- ✅ Check file path: custom-sw.js should be at root
- ✅ Clear browser cache and reload

### "Notification permission denied"
- ✅ Click lock icon in address bar
- ✅ Notifications > Allow
- ✅ Refresh page

### Notification doesn't appear
- ✅ Check service worker is "activated" in DevTools
- ✅ Check notification is in IndexedDB
- ✅ Wait for check interval (10 seconds in test mode)
- ✅ Verify system notifications are enabled (OS level)

## Quick Debug Commands

**In Browser Console:**
```javascript
// Check if service worker registered
navigator.serviceWorker.getRegistration().then(reg => console.log(reg));

// Check notification permission
console.log(Notification.permission);

// Check timer state
console.log(JSON.parse(localStorage.getItem('bgl-ketone-timer')));

// Unregister service worker (for clean testing)
navigator.serviceWorker.getRegistration().then(reg => reg.unregister());
```

**Manually Trigger Notification (in Service Worker console):**
```javascript
// Open DevTools > Application > Service Workers > "Source" link for custom-sw.js
// In that console:
checkScheduledNotifications();
```

## Production Reminder

**BEFORE DEPLOYING TO PRODUCTION:**

1. Revert timer to 2 hours:
   ```typescript
   this.remainingSeconds = 2 * 60 * 60;
   ```

2. Revert check interval to 1 minute:
   ```javascript
   const NOTIFICATION_CHECK_INTERVAL = 60000;
   ```

3. Test on HTTPS (service workers require HTTPS in production)

4. Rebuild for production:
   ```powershell
   cd client
   npx ng build --configuration production
   # Output is in dist/Vitara/browser/
   http-server dist/Vitara/browser -p 8080 -c-1
   ```
