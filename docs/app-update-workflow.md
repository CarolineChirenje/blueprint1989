# App Update Workflow

Divvy is a PWA (Progressive Web App). Unlike traditional app-store apps, the browser controls when cached assets are refreshed. This document describes how app updates are detected, surfaced to the user, and applied.

---

## Overview

When a new build is deployed to the server, the Angular Service Worker (SW) running in the user's browser detects that the cached assets have changed. At that point Divvy shows an update banner at the bottom of the screen. The banner has two modes depending on whether the update is routine or breaking.

---

## The Version Manifest (`version.json`)

A small JSON file at the root of the app (`/version.json`) controls how updates behave. It is **intentionally excluded from the service worker cache** so it is always fetched fresh from the server.

```json
{
  "version": "1.0.0",
  "breaking": false,
  "minRequired": "0.0.0"
}
```

| Field | Type | Purpose |
|---|---|---|
| `version` | string | The current deployed version. For reference only. |
| `breaking` | boolean | `true` → the update contains breaking changes; users cannot skip it. |
| `minRequired` | string | Minimum version that can safely run. If a user's running version is older, the breaking banner is shown on startup even if the SW has not yet delivered a new version. |

**To ship a breaking update**, set `"breaking": true` (and optionally bump `"minRequired"`) before deploying.

---

## Update Detection Flow

```
User opens / uses app
        │
        ▼
SW checks for updated build
  (immediate check on startup + every 6 h)
        │
        ├── No change detected → nothing shown
        │
        └── VERSION_READY event fired
                │
                ▼
        Fetch /version.json?_=<timestamp>   ← cache: no-store
                │
                ├── breaking: true  OR  running version < minRequired
                │       └──► Show BREAKING update banner (forced)
                │
                └── breaking: false  AND  running version ≥ minRequired
                        └──► Show STANDARD update banner (dismissible)
```

### Staleness check on startup

In addition to the SW event, `checkStalenessOnStartup()` runs every time a logged-in user loads the app. It fetches `version.json` and compares `environment.version` against `minRequired`. If the running version is too old, the breaking banner is shown immediately — even before the SW has had a chance to download the new build. This catches users who haven't opened the app in a long time.

---

## Banner Types

### Standard Update (green)

Shown when `breaking: false` and the running version satisfies `minRequired`.

```
┌─────────────────────────────────────────────────────────┐
│  ▶  New version available                               │
│     A new version of Divvy is ready to install.         │
│                  [Update Now]  [Later]  [What's New?]   │
└─────────────────────────────────────────────────────────┘
```

| Button | Action |
|---|---|
| **Update Now** | Activates the new SW, clears caches, reloads the page. |
| **Later** | Dismisses the banner for the current session. The banner may reappear on the next page load. |
| **What's New?** | Navigates to `/release-notes` without applying the update. |

### Breaking Update (orange)

Shown when `breaking: true` **or** the running version is older than `minRequired`.

```
┌─────────────────────────────────────────────────────────┐
│  ⚠  Critical update required                           │
│     This update contains breaking changes. Skipping    │
│     it may cause features to stop working.             │
│                                    [Update Now]        │
└─────────────────────────────────────────────────────────┘
```

- **No "Later" button** — the update cannot be deferred.
- The banner persists until the user taps **Update Now**.
- Relevant for mobile users where stale API contracts can silently break data submissions (e.g. expense entries, payments).

---

## Applying an Update

Tapping **Update Now** calls `swUpdate.activateUpdate()` followed by `window.location.reload()`. This:

1. Instructs the SW to swap the active cache to the newly downloaded version.
2. Reloads the page so all JS/CSS assets are served from the fresh cache.

---

## How to Ship Updates

### Routine update (no breaking changes)

1. Build and deploy as normal — no changes to `version.json` required.
2. The SW will detect the new build within 6 hours (or immediately on next app open).
3. Users will see the standard green banner.

### Breaking update

1. Set `"breaking": true` in `client/src/version.json`.
2. Optionally bump `"minRequired"` to the new version string (e.g. `"1.2.0"`) to catch users who have not opened the app in a long time.
3. Build and deploy.
4. Users will see the orange forced-update banner immediately, with no option to defer.
5. After deployment is verified stable, reset `"breaking": false` in the next routine release.

---

## Why `version.json` is Never Cached

`version.json` is placed at the dist root (`/version.json`). The Angular SW (`ngsw-config.json`) only caches:

- `/*.js`, `/*.css` — compiled app bundles
- `/assets/**` — static assets

`version.json` matches none of these patterns; it is also not listed in the SW manifest (`ngsw.json`). Every fetch of `/version.json` therefore goes directly to the origin server, bypassing both the SW cache and the browser cache (via `cache: 'no-store'` in the fetch call).

---

## Related Files

| File | Purpose |
|---|---|
| `client/src/version.json` | Update manifest — edit `breaking` / `minRequired` before deploying a breaking change |
| `client/src/app/app.component.ts` | `fetchVersionManifest()`, `isVersionOlder()`, `checkStalenessOnStartup()`, `applyUpdate()`, `dismissUpdatePrompt()` |
| `client/src/app/app.component.html` | Update banner template — `@if (showUpdatePrompt)` block |
| `client/src/app/app.component.css` | `.pwa-update-banner` and `.breaking` modifier styles |
| `client/src/ngsw-config.json` | SW cache rules — intentionally does not include `version.json` |
| `client/src/environments/environment*.ts` | `version` field — compared against `minRequired` on startup |
