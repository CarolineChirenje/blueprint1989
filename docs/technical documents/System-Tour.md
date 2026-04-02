# System Tour

## Overview

Vitara includes a guided in-app tour powered by [Shepherd.js](https://shepherdjs.dev/). The tour walks users through the key areas of the application relevant to their role. It launches automatically the first time a user lands on the dashboard after login, and can be replayed at any time from the Help menu.

A companion **What's New** mini-tour fires once per version update, surfacing new features to returning users without repeating the full onboarding flow.

---

## Role Access

| Feature | All Roles | Admin / SuperAdmin |
|---|---|---|
| Main system tour (auto-launch on first login) | ✓ | ✓ |
| Replay tour from Help → Take a Tour | ✓ | ✓ |
| What's New tour on version update | ✓ | ✓ |
| Admin-specific tour steps (Management menu) | — | ✓ |
| Care-user-specific tour steps (Dashboard, BGL, BP, Meals, Incidents) | ✓ (non-admin) | — |

---

## Main System Tour

### Behaviour

- Automatically launched the first time a logged-in user navigates to `/dashboard`.
- Only fires once — after the user completes or skips the tour, `HasCompletedTour` is persisted to the database and the tour will not auto-launch again.
- Can be relaunched at any time via **Help → Take a Tour** in the navigation bar (the completion flag is ignored on manual relaunch).
- Fully responsive: on mobile viewports (< 768 px) steps detach from their DOM anchors and display as centred overlays. The hamburger menu is opened automatically when a nav-menu step is shown.
- Ends with a dedicated **"Inspired by Arya ❤️"** final step.

### Care-User Tour Steps

| Step | Anchor | Description |
|---|---|---|
| Welcome | — (centred) | Introduction to Vitara |
| Navigation Menu | `nav` | Overview of the app navigation |
| Dashboard | `#nav-dashboard` | Summary of recent readings and trends |
| Diabetes Monitoring | `#nav-assessment` | BGL recording and assessment history |
| Blood Pressure | `#nav-blood-pressure` | BP session logging and charts |
| Nutrition | `#nav-meals` | Meal entry and carb tracking |
| Incidents | `#nav-incidents` | Health incident reporting |
| Notifications | `#notification-bell` | In-app alerts and reminders |
| Your Profile | `#user-menu` | Account settings and security |
| You are all set! | — (centred) | Completion with feedback note |
| Inspired by Arya | — (centred) | Closing "Inspired by Arya ❤️" step |

### Admin Tour Steps

| Step | Anchor | Description |
|---|---|---|
| Welcome | — (centred) | Introduction to Vitara Administration |
| Navigation | `nav` | Overview of the admin navigation |
| Dashboard | `#nav-dashboard` | System activity and metrics |
| Management | `#nav-management` | Admin control centre |
| Notifications | `#notification-bell` | System alerts and push preferences |
| Your Profile | `#user-menu` | Account settings and MFA |
| You are ready to go! | — (centred) | Completion with feedback note |
| Inspired by Arya | — (centred) | Closing "Inspired by Arya ❤️" step |

---

## What's New Tour

### Behaviour

- Fires automatically on the user's first visit to `/dashboard` after the app has been updated to a new version.
- Only shown to users who have already completed the main system tour — first-time users see the main tour instead.
- Each step includes a **Skip** button so users can dismiss the tour at any point.
- Acknowledging or cancelling the tour persists the current version as `LastSeenVersion` on the user record. The tour will not reappear until the next version bump.

### Steps (v1.1.0)

| Step | Content |
|---|---|
| What's New in v1.1.0 | Overview slide announcing the update |
| Blood Pressure Incidents | New BP incident logging workflow |
| Offline Support | Offline queue and automatic sync |
| That's everything! | Closing slide — "Inspired by Arya ❤️" |

---

## Backend

### Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/tour/complete` | Bearer | Marks `HasCompletedTour = true` for the current user |
| `POST` | `/api/auth/seen-version` | Bearer | Sets `LastSeenVersion` to the supplied version string |

#### `POST /api/auth/seen-version` request body

```json
{ "version": "1.1.0" }
```

### User Entity Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `HasCompletedTour` | `bool` | `false` | Set to `true` when the main tour is completed or skipped |
| `LastSeenVersion` | `string?` | `null` | The most recent app version the user has acknowledged via the What's New tour |

### DTOs

`AuthResponse` includes both fields so they are available immediately after login without a separate API call:

```csharp
public bool HasCompletedTour { get; set; } = false;
public string? LastSeenVersion { get; set; }
```

### Migrations

| Migration | Description |
|---|---|
| `AddHasCompletedTour` | Adds `HasCompletedTour bool NOT NULL DEFAULT false` to the `users` table |
| `AddLastSeenVersion` | Adds `LastSeenVersion varchar(20) NULL` to the `users` table |

---

## Frontend

### Key Files

| File | Purpose |
|---|---|
| `client/src/app/core/services/tour.service.ts` | All tour logic — Shepherd.js integration, role-specific step builders, `startTour()`, `startWhatsNewTour()` |
| `client/src/app/app.component.ts` | Auto-launch wiring: `launchTour()` and `launchWhatsNew()` called from the NavigationEnd subscriber |
| `client/src/app/app.component.html` | Nav element IDs (`#nav-dashboard`, `#nav-assessment`, etc.) used as step anchors; Help → Take a Tour link |
| `client/src/styles.css` | Shepherd.js CSS import + full Vitara blue theme override; `.shepherd-inspired` class for the final step |
| `client/src/app/shared/models/user.model.ts` | `hasCompletedTour?: boolean` and `lastSeenVersion?: string \| null` on the `User` interface |
| `client/src/app/auth/login.component.ts` | Persists `hasCompletedTour` and `lastSeenVersion` to local/session storage on login and MFA login |

### Tour Service API

```typescript
// Start the full role-aware onboarding tour
tourService.startTour(role: Role | undefined, callbacks: TourCallbacks): void

// Start the What's New mini-tour for a specific version
tourService.startWhatsNewTour(version: string, role: Role | undefined, callbacks: TourCallbacks): void

interface TourCallbacks {
  openMenu: () => void;   // called to open the mobile nav when a nav step is shown
  closeMenu: () => void;  // called when the tour ends
}
```

### Auto-Launch Logic (AppComponent)

```typescript
// Inside the NavigationEnd subscriber, on /dashboard arrival:
if (user && !user.hasCompletedTour) {
  this.launchTour();                                          // first-time user
} else if (user && user.hasCompletedTour && user.lastSeenVersion !== environment.version) {
  this.launchWhatsNew();                                      // returning user after version bump
}
```

### Shepherd.js Import Pattern

`Tour` is not a named runtime export from `shepherd.js`. The correct import pattern is:

```typescript
import Shepherd from 'shepherd.js';                                       // default export (runtime)
import type { StepOptions, StepOptionsButton, PopperPlacement } from 'shepherd.js'; // types only

// Instantiate tour:
this.tour = new (Shepherd as any).Tour({ useModalOverlay: true, ... });
```

### Nav Element IDs

The following `id` attributes are set in `app.component.html` as tour step anchors:

| ID | Element |
|---|---|
| `nav-dashboard` | Dashboard nav link |
| `nav-assessment` | Diabetes / BGL nav link |
| `nav-meals` | Nutrition nav link |
| `nav-blood-pressure` | Blood Pressure nav link |
| `nav-incidents` | Incidents nav link |
| `nav-management` | Management dropdown (admin only) |
| `help-menu-btn` | Help menu button |
| `notification-bell` | Notification bell icon |
| `user-menu` | User profile avatar/button |

---

## Adding New What's New Steps for a Future Release

1. Bump `version` in `client/src/environments/environment.ts` and `client/src/version.json`.
2. In `tour.service.ts`, update `buildWhatsNewSteps()` to add new steps describing the changes in the new version.
3. Update the `What's New Tour` section of this document with the new step content.
4. No backend changes are required — `LastSeenVersion` stores the version string dynamically.
