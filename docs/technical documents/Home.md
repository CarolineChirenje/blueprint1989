# Batanai — Technical Documentation

Welcome to the Batanai developer wiki. This covers every feature of the Batanai expense-sharing platform in detail, including backend (.NET 10 API) and frontend (Angular 21 PWA) implementation for each feature.

---

## Architecture Quick Reference

| Layer | Technology |
|---|---|
| Frontend | Angular 21, Angular Material, Progressive Web App (PWA) |
| Backend | ASP.NET Core 10, Entity Framework Core, PostgreSQL |
| Authentication | JWT Bearer, WebAuthn/FIDO2 (Fido2NetLib), TOTP MFA |
| Push Notifications | Web Push (VAPID), Service Worker |
| Offline Support | IndexedDB offline queue, Service Worker caching |
| Background Jobs | .NET Hosted Services (IHostedService) |

## User Roles

| Role ID | Role | Access Level |
|---|---|---|
| 1 | SuperAdmin | Full system access |
| 2 | Admin | Manage users, cycles, app config |
| 3 | Member | Participate in cycles, add expenses, submit payments |

---

## Feature Documentation

| # | Feature | Summary |
|---|---|---|
| 1 | [Authentication](Authentication.md) | Login, signup, MFA, password reset, JWT token lifecycle |
| 2 | [Biometric Authentication](Biometric-Authentication.md) | WebAuthn/FIDO2 passwordless login and credential management |
| 3 | [Dashboard](Dashboard.md) | Role-based navigation cards and time-of-day greeting |
| 4 | [Expense Cycles](Cycles.md) | Expense cycle management, member balances, close workflow |
| 5 | [Profile](Profile.md) | Profile sub-pages: name/password, MFA, biometric, devices, reports, notifications |
| 6 | [Notifications](Notifications.md) | In-app notification bell inbox, Web Push pipeline, and per-user preferences |
| 7 | [Management (Admin Hub)](Management.md) | Centralised admin hub: users, cycles, reports, app config |
| 8 | [Offline Queue](Offline-Queue.md) | IndexedDB offline queue with 24-hour TTL and auto-sync on reconnect |
| 9 | [Push Notifications](Push-Notifications.md) | VAPID Web Push pipeline and service worker handler |
| 10 | [Feature & Bug Reports](Feature-Bug-Reports.md) | User-submitted feature requests and bug reports with admin review workflow |
| 11 | [App Configuration](App-Configuration.md) | Runtime key-value configuration store with secret masking and bulk update |
| 12 | [Business Overview](Business-Overview.md) | Platform summary: problem, solution, market, and revenue model |

---

## Setting Up the Wiki

These docs live in `docs/technical documents/` in the main repository.

To publish / update the GitHub Wiki:

```bash
# Clone the wiki repo (separate from the main repo)
git clone https://github.com/CarolineChirenje/Batanai.wiki.git
cd Batanai.wiki

# Copy all docs — rename files to match wiki page names (no spaces, use hyphens)
# Home.md        ? this file (wiki landing page)
# Authentication.md
# Biometric-Authentication.md
# Dashboard.md
# Cycles.md
# ... etc

# Stage, commit, push
git add .
git commit -m "docs: update wiki"
git push
```
