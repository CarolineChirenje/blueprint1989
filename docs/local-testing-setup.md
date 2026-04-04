# Local PWA Testing Setup

Step-by-step guide to run the Divvy PWA locally and test it on an Android phone — exactly as configured.

---

## Prerequisites (install once globally)

```powershell
npm install -g http-server
npm install -g ngrok
```

**ngrok auth token (one-time setup):**
1. Sign up free at [dashboard.ngrok.com](https://dashboard.ngrok.com)
2. Copy your token from [dashboard.ngrok.com/get-started/your-authtoken](https://dashboard.ngrok.com/get-started/your-authtoken)
3. Run:
   ```powershell
   ngrok config add-authtoken YOUR_TOKEN_HERE
   ```

---

## Every Session — Start Order

You need **four things running simultaneously**, each in its own terminal.

---

### Terminal 1 — .NET API

```powershell
Set-Location C:\dev\divvy\server\src\Divvy.Api
dotnet run
```

Wait until you see `Now listening on: http://localhost:5000` before continuing.

---

### Terminal 2 — Build and serve the Angular client

```powershell
# Build first (required — ng serve does NOT work with service workers)
Set-Location C:\dev\divvy\client
npx ng build

# Then serve the output on port 80 (matches ngrok config)
http-server C:\dev\divvy\client\dist\Divvy\browser -p 80 -c-1
```

> **Important:** Always rebuild (`npx ng build`) after any code change before re-serving. The `-c-1` flag disables caching so the browser always gets fresh files.

---

### Terminal 3 — ngrok tunnel (client → HTTPS)

```powershell
ngrok http 80
```

ngrok will print a line like:

```
Forwarding   https://uncommanderlike-simonne-demographical.ngrok-free.dev -> http://localhost:80
```

Copy the `https://` URL — this is what you open on your Android phone.

> **Note:** Free ngrok URLs change every time you restart ngrok. When you get a new URL, update [Program.cs](../server/src/Divvy.Api/Program.cs) CORS policy (see step below) and restart the API.

---

### VS Code Ports Panel — API tunnel (port 5000 → HTTPS)

1. In VS Code, click the **Ports** tab in the bottom panel
2. Click **Forward a Port** → type `5000` → press Enter
3. VS Code creates an `https://XXXXXXX-5000.aue.devtunnels.ms` URL
4. Right-click the port 5000 row → **Port Visibility** → **Public**

> The tunnel must be **Public** — Private tunnels require Microsoft login and won't work on your phone.

---

## When the ngrok URL changes

Update the CORS allowed origins in [server/src/Divvy.Api/Program.cs](../server/src/Divvy.Api/Program.cs):

```csharp
policy.WithOrigins(
    "http://localhost:4200", "http://localhost:4201", "http://localhost:3000",
    "http://localhost:8080", "http://localhost:80",
    "https://YOUR-NEW-NGROK-URL.ngrok-free.dev",       // ← update this
    "https://XXXXXXX-5000.aue.devtunnels.ms",
    "https://XXXXXXX-8080.aue.devtunnels.ms")
```

Then restart the API (Terminal 1).

---

## When the devtunnel URL changes

The VS Code devtunnel URL changes if VS Code restarts. When it does:

1. Update `apiUrl` in [client/src/environments/environment.ts](../client/src/environments/environment.ts):
   ```typescript
   apiUrl: 'https://NEW-DEVTUNNEL-5000.aue.devtunnels.ms/api',
   excelExportUrl: 'https://NEW-DEVTUNNEL-5000.aue.devtunnels.ms/api/export',
   ```
2. Update CORS in `Program.cs` with the new devtunnel origin
3. Rebuild the client: `npx ng build`
4. Restart the API: `dotnet run`

---

## Testing on Android (Chrome) — Add to Home Screen

1. Open Chrome on your Android phone
2. Navigate to the ngrok URL:
   ```
   https://uncommanderlike-simonne-demographical.ngrok-free.dev
   ```
3. If ngrok shows a warning page ("You are about to visit...") → tap **Visit Site**
4. Log in to the app
5. After ~30 seconds, Chrome shows an **"Add Divvy to Home Screen"** banner
   - Or tap **⋮ menu → Add to Home screen**
6. Tap **Add** — the app icon appears on your home screen
7. Launch from the home screen → app opens in standalone mode (no browser chrome)

> **Tip:** If the install banner never appears, open `chrome://flags/#bypass-app-banner-engagement-checks` on the phone and enable it to skip Chrome's repeated-visit threshold.

---

## Verify the Service Worker is Running

On laptop Chrome at `http://localhost:8080`:

1. Open DevTools (`F12`) → **Application** → **Service Workers**
2. Confirm `custom-sw.js` shows **Status: activated and running**
3. Check **Application → Manifest** — all 4 PNG icons should preview (192 any, 512 any, 192 maskable, 512 maskable)
4. The ⊕ install icon in the address bar confirms Chrome considers the app installable

**Force SW update after a rebuild:**
```javascript
// Paste in DevTools Console, then reload the page:
navigator.serviceWorker.getRegistrations().then(regs => regs.forEach(r => r.unregister()));
```

---

## Verify Everything is Running

```powershell
# API on port 5000
netstat -ano | findstr ":5000" | findstr "LISTENING"

# http-server on port 80
netstat -ano | findstr ":80 " | findstr "LISTENING"

# ngrok active tunnel URL
Invoke-RestMethod http://127.0.0.1:4040/api/tunnels | Select-Object -ExpandProperty tunnels | Select-Object public_url
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| "Target machine actively refused" on phone | http-server not running, or ngrok pointing at wrong port | Run `http-server dist/Divvy/browser -p 80 -c-1`; confirm ngrok forwards port 80 |
| "No web page was found" for devtunnel | VS Code tunnel is Private | Ports panel → right-click port 5000 → Port Visibility → **Public** |
| CORS error on login | New ngrok/devtunnel URL not added to `Program.cs` | Update CORS origins, restart API |
| SW not updating after rebuild | Old SW is cached | Unregister SW in DevTools Console (see above), then reload |
| ngrok "endpoint already online" error | ngrok is already running — this is fine | Ignore the error; use existing tunnel URL from `http://localhost:4040` |
| ngrok popup closes instantly | Auth token not configured | Run `ngrok config add-authtoken YOUR_TOKEN` |
| Icons not showing in Manifest tab | Stale dist | Rebuild (`npx ng build`), unregister SW, reload |

---

## Summary — What's Running Where

| Service | Port | Accessible at |
|---|---|---|
| .NET API | 5000 | `http://localhost:5000` (laptop) / `https://XXXX-5000.aue.devtunnels.ms` (phone) |
| Angular client | 80 | `http://localhost:80` (laptop) / ngrok HTTPS URL (phone) |
| ngrok tunnel | — | `https://YOUR-URL.ngrok-free.dev` → phone |
| VS Code devtunnel | — | `https://XXXX-5000.aue.devtunnels.ms` → phone API |

