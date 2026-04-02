# Vitara — Technical Documentation

Welcome to the Vitara developer wiki. This covers every feature of the Vitara health management platform in detail, including backend (.NET 10 API) and frontend (Angular 21 PWA) implementation for each feature.

---

## Architecture Quick Reference

| Layer | Technology |
|---|---|
| Frontend | Angular 21, Angular Material, Progressive Web App (PWA) |
| Backend | ASP.NET Core 10, Entity Framework Core, PostgreSQL |
| Authentication | JWT Bearer, WebAuthn/FIDO2 (Fido2NetLib), TOTP MFA |
| Push Notifications | Web Push (VAPID), Service Worker |
| Offline Support | IndexedDB offline queue, Service Worker caching |
| Export | ClosedXML (.xlsx), Google Drive API v3 |
| Background Jobs | .NET Hosted Services (IHostedService) |

## User Roles

| Role ID | Role | Access Level |
|---|---|---|
| 1 | SuperAdmin | Full system access |
| 2 | Administrator | Full access except system-level config |
| 3 | Carer | Clinical entry for linked Care Recipients |
| 4 | SupportWorker | Clinical entry for linked Care Recipients |
| 5 | CareRecipient | Own data only |
| 6 | HealthCareProvider | Read access for linked Care Recipients |

---

## Feature Documentation

| # | Feature | Summary |
|---|---|---|
| 1 | [Authentication](Authentication.md) | Login, signup, MFA, password reset, JWT token lifecycle |
| 2 | [Biometric Authentication](Biometric-Authentication.md) | WebAuthn/FIDO2 passwordless login and credential management |
| 3 | [BGL Assessment](BGL-Assessment.md) | Guided blood glucose assessment state machine with hypo/hyper protocols |
| 4 | [Blood Pressure Monitoring](Blood-Pressure-Monitoring.md) | Multi-reading BP session workflow with AHA classification |
| 5 | [Incidents — Diabetes](Incidents-Diabetes.md) | Diabetes incident recording, auto-classification, and push alerts |
| 6 | [Incidents — Blood Pressure](Incidents-Blood-Pressure.md) | BP incident recording with HBP guard, classification, and push alerts |
| 7 | [Meal Entry](Meal-Entry.md) | Meal, carbohydrate, and bolus tracking with history and export |
| 8 | [Dashboard](Dashboard.md) | Role-conditional navigation cards and time-of-day greeting |
| 9 | [Profile](Profile.md) | All profile sub-pages: name/password, MFA, biometric, devices, conditions, reports, notifications |
| 10 | [Care Recipients](Care-Recipients.md) | Carer–Care Recipient linking workflow and admin delink management |
| 11 | [Notifications](Notifications.md) | In-app notification bell inbox, Web Push pipeline, and per-user preferences |
| 12 | [Supplies](Supplies.md) | Supply tracking, quantity projection algorithm, catalog management, and weekly email cron |
| 13 | [Management](Management.md) | Centralised admin hub covering all 11 management sub-pages |
| 14 | [Export & Reports](Export-Reports.md) | Excel report generation (ClosedXML) and optional Google Drive upload |
| 15 | [Offline Queue](Offline-Queue.md) | IndexedDB offline queue with 24-hour TTL and auto-sync on reconnect |
| 16 | [Push Notifications](Push-Notifications.md) | VAPID Web Push pipeline, background timer reminders, and service worker handler |
| 17 | [Classification Management](Classification-Management.md) | BGL, ketone, blood pressure, and pulse rate classification range management |
| 18 | [Cycles](Cycles.md) | Care cycle management and JWT term binding |
| 19 | [Feature & Bug Reports](Feature-Bug-Reports.md) | User-submitted feature requests and bug reports with admin review workflow |
| 20 | [App Configuration](App-Configuration.md) | Runtime key-value configuration store with secret masking and bulk update |
| 21 | [Google Drive Integration](Google-Drive-Integration.md) | OAuth 2.0 setup, token storage, upload flow, and admin configuration guide |
| 22 | [Business Overview](Business-Overview.md) | Investor-facing platform summary: problem, solution, market, revenue model, and traction |


---

## Architecture Quick Reference

| Layer | Technology |
|---|---|
| Frontend | Angular 21, Angular Material, Progressive Web App (PWA) |
| Backend | ASP.NET Core 10, Entity Framework Core, PostgreSQL |
| Authentication | JWT Bearer, WebAuthn/FIDO2 (Fido2NetLib), TOTP MFA |
| Push Notifications | Web Push (VAPID), Service Worker |
| Offline Support | IndexedDB offline queue, Service Worker caching |
| Export | ClosedXML (.xlsx), Google Drive API v3 |
| Background Jobs | .NET Hosted Services (IHostedService) |

## User Roles

| Role ID | Role | Access Level |
|---|---|---|
| 1 | SuperAdmin | Full system access |
| 2 | Administrator | Full access except system-level config |
| 3 | Carer | Clinical entry for linked Care Recipients |
| 4 | SupportWorker | Clinical entry for linked Care Recipients |
| 5 | CareRecipient | Own data only |
| 6 | HealthCareProvider | Read access for linked Care Recipients |

---

## Feature Documentation

| # | Page | Feature Summary |
|---|---|---|
| 1 | [[Authentication]] | Login, signup, MFA, password reset, JWT token lifecycle |
| 2 | [[Biometric-Authentication]] | WebAuthn/FIDO2 passwordless login and credential management |
| 3 | [[BGL-Assessment]] | Guided blood glucose assessment state machine with hypo/hyper protocols |
| 4 | [[Blood-Pressure-Monitoring]] | Multi-reading BP session workflow with AHA classification |
| 5 | [[Incidents-Diabetes]] | Diabetes incident recording, auto-classification, and push alerts |
| 6 | [[Incidents-Blood-Pressure]] | BP incident recording with HBP guard, classification, and push alerts |
| 7 | [[Meal-Entry]] | Meal, carbohydrate, and bolus tracking with history and export |
| 8 | [[Dashboard]] | Role-conditional navigation cards and time-of-day greeting |
| 9 | [[Profile]] | All profile sub-pages: name/password, MFA, biometric, devices, conditions, reports, notifications |
| 10 | [[Care-Recipients]] | Carer–Care Recipient linking workflow and admin delink management |
| 11 | [[Notifications]] | In-app notification bell inbox, Web Push pipeline, and per-user preferences |
| 12 | [[Supplies]] | Supply tracking, quantity projection algorithm, catalog management, and weekly email cron |
| 13 | [[Management]] | Centralised admin hub covering all 11 management sub-pages |
| 14 | [[Export-Reports]] | Excel report generation (ClosedXML) and optional Google Drive upload |
| 15 | [[Offline-Queue]] | IndexedDB offline queue with 24-hour TTL and auto-sync on reconnect |
| 16 | [[Push-Notifications]] | VAPID Web Push pipeline, background timer reminders, and service worker handler |
| 17 | [[Classification-Management]] | BGL, ketone, blood pressure, and pulse rate classification range management |
| 18 | [[Cycles-Terms]] | Care cycle management and JWT term binding |
| 19 | [[Feature-Bug-Reports]] | User-submitted feature requests and bug reports with admin review workflow |
| 20 | [[App-Configuration]] | Runtime key-value configuration store with secret masking and bulk update |

---

## Setting Up the Wiki

These docs live in `docs/technical documents/` in the main repository.

To publish / update the GitHub Wiki:

```bash
# Clone the wiki repo (separate from the main repo)
git clone https://github.com/CarolineChirenje/vitara.wiki.git
cd vitara.wiki


# Copy all docs — rename files to match wiki page names (no spaces, use hyphens)
# Home.md        ← this file (wiki landing page)
# Authentication.md
# Biometric-Authentication.md
# BGL-Assessment.md
# ... etc

# Stage, commit, push
git add .
git commit -m "Update technical docs"
git push
```

> **Tip:** GitHub Wiki page names use the filename without `.md`. Spaces in titles become hyphens in the URL. The `[[Page-Name]]` double-bracket syntax in this file creates automatic wiki links.
