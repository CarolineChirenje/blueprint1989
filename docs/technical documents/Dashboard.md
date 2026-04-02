# Dashboard

## Overview

The Dashboard is the default landing page for all authenticated users. It presents a personalised greeting and a grid of navigation cards that direct users to the features available for their role. The cards shown are conditionally rendered based on the user's role and the care recipient's active health conditions, ensuring that users only see features relevant to their scope of care.

---

## Role Access

The Dashboard is accessible to all authenticated roles. The set of cards visible varies per role (see below).

---

## Backend

The Dashboard itself has no dedicated backend endpoint. It reads data from:
- The authenticated user's JWT claims (role, firstName, termId).
- `GET /auth/my-conditions` (or conditions embedded in the login response) — used to populate the `AuthService.accessibleConditions$` BehaviorSubject.

No server call is made on Dashboard load beyond what is cached from login.

---

## Frontend

### Route

| Path | Component | Guard |
|---|---|---|
| `/dashboard` | `DashboardComponent` | `AuthGuard` |

This is the **default route** — navigating to `/` redirects to `/dashboard` after authentication.

### `DashboardComponent` — `/dashboard`

**File:** `client/src/app/features/dashboard/components/dashboard.component.ts`

#### Greeting Card
- Displays a time-of-day greeting ("Good morning / afternoon / evening, [firstName]!").
- The greeting text updates every second via `setInterval` (to reflect greeting changes at time boundaries without requiring a page reload).
- `firstName` is read from `AuthService.getUserName()`.

#### Navigation Cards

Each card is a clickable tile linking to a feature route. Cards are displayed and hidden based on:

1. **Role** — evaluated via `AuthService.getUserRoleId()`.
2. **Conditions** — evaluated via `AuthService.hasDiabetesConditions()` and `AuthService.hasHbpConditions()`, which read from `localStorage` conditions data set on login.

| Card | Required Role / Condition | Target Route |
|---|---|---|
| BGL Assessment | SuperAdmin / Admin, OR `hasDiabetesConditions()` | `/assessment` |
| Meal Entry | SuperAdmin / Admin, OR `hasDiabetesConditions()` | `/meal-entry/history` |
| Incidents | SuperAdmin / Admin, OR `hasDiabetesConditions()` OR `hasHbpConditions()` | `/incidents` |
| Blood Pressure | SuperAdmin / Admin, OR `hasHbpConditions()` | `/blood-pressure` |
| Supplies | All except HealthCareProvider | `/supplies` |
| Users | SuperAdmin / Admin only | `/management/users` |
| Terms/Cycles | SuperAdmin / Admin only | `/management/cycles` |

`showDiabetes()`, `showIncidentsCard()`, `showBloodPressure()` are helper methods on the component.

#### `showIncidentsCard()`
```typescript
showIncidentsCard(): boolean {
  return this.isAdminOrHigher()
    || this.hasDiabetesConditions()
    || this.hasHbpConditions();
}
```
The Incidents card is rendered outside the diabetes-only block so it appears for HBP-only care recipients.

#### Route Map
A `routeMap` object in the component maps string card keys to Angular router paths, keeping the template clean:

```typescript
routeMap = {
  assessment: '/assessment',
  mealEntry: '/meal-entry/history',
  incidents: '/incidents',
  bloodPressure: '/blood-pressure',
  supplies: '/supplies',
  users: '/management/users',
  cycles: '/management/cycles'
};
```

#### Navigation
Clicking a card calls `this.router.navigate([routeMap[key]])`.

---

## Condition Gating Logic

Conditions are stored in `localStorage` as a JSON array under the `userConditions` key after login. The `AuthService` provides:

```typescript
hasDiabetesConditions(): boolean
  // returns true if any condition in userConditions is T1D or T2D

hasHbpConditions(): boolean
  // returns true if any condition in userConditions is HBP
```

For Admin and SuperAdmin roles, both `hasDiabetesConditions()` and `hasHbpConditions()` return `true` unconditionally — admins see all feature cards.

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
  |                         | read conditions from localStorage|
  |                         | compute visible cards       |
  |                         |                             |
  |<-- greeting + cards ----|                             |
  |                         |                             |
  | Click "BGL Assessment"  |                             |
  |                         | router.navigate(['/assessment'])|
  |<-- /assessment page ----|                             |
```
