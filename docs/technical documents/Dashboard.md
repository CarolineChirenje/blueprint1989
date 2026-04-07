# Dashboard

## Overview

The Dashboard is the default landing page for all authenticated users. It presents a personalised greeting and a grid of navigation cards that direct users to the features available for their role.

---

## Role Access

The Dashboard is accessible to all authenticated roles. The set of cards visible varies per role (see below).

---

## Backend

The Dashboard itself has no dedicated backend endpoint. It reads data from:
- The authenticated user's JWT claims (role, firstName).

No server call is made on Dashboard load beyond what is cached from login.

---

## Frontend

### Route

| Path | Component | Guard |
|---|---|---|
| `/dashboard` | `DashboardComponent` | `AuthGuard` |

This is the **default route** â€” navigating to `/` redirects to `/dashboard` after authentication.

### `DashboardComponent` â€” `/dashboard`

**File:** `client/src/app/features/dashboard/components/dashboard.component.ts`

#### Greeting Card
- Displays a time-of-day greeting ("Good morning / afternoon / evening, [firstName]!").
- The greeting text updates every second via `setInterval` (to reflect greeting changes at time boundaries without requiring a page reload).
- `firstName` is read from `AuthService.getUserName()`.

#### Navigation Cards

Each card is a clickable tile linking to a feature route. Cards are displayed based on the user's role, evaluated via `AuthService.getUserRoleId()`.

| Card | Required Role | Target Route |
|---|---|---|
| Active Cycle | All roles | `/cycles` |
| My Obligations | All roles | `/cycles/obligations` |
| Expenses | All roles | `/expenses` |
| Payments | All roles | `/payments` |
| Users | SuperAdmin / Admin only | `/management/users` |
| Manage Cycles | SuperAdmin / Admin only | `/management/cycles` |

#### Route Map
A `routeMap` object in the component maps string card keys to Angular router paths, keeping the template clean:

```typescript
routeMap = {
  cycles: '/cycles',
  obligations: '/cycles/obligations',
  expenses: '/expenses',
  payments: '/payments',
  users: '/management/users',
  manageCycles: '/management/cycles'
};
```

#### Navigation
Clicking a card calls `this.router.navigate([routeMap[key]])`.

---

---

## End-to-End Data Flow

```
User (any role)             Angular                      API
  |                         |                             |
  | Navigate to /           |                             |
  |                         | AuthGuard: check JWT valid  |
  |                         | redirect to /dashboard      |
  |                         |                             |
  | Dashboard renders       |                             |
  |                         | read role from JWT          |
  |                         | compute visible cards       |
  |                         |                             |
  |<-- greeting + cards ----|                             |
  |                         |                             |
  | Click "BGL Assessment"  |                             |
  |                         | router.navigate(['/assessment'])|
  |<-- /assessment page ----|                             |
```
