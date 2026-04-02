# Vitara — Technical Documentation

This folder contains detailed technical documentation for every feature in the Vitara health management platform. Each document covers both the backend (.NET 10 API) and frontend (Angular 21 PWA) implementation for its respective feature.

---

## Table of Contents

| # | Document | Feature Summary |
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
| 13 | [Management (Admin Hub)](Management.md) | Centralised admin hub covering all 11 management sub-pages |
| 14 | [Export & Reports](Export-Reports.md) | Excel report generation (ClosedXML) and optional Google Drive upload |
| 15 | [Offline Queue](Offline-Queue.md) | IndexedDB offline queue with 24-hour TTL and auto-sync on reconnect |
| 16 | [Push Notifications](Push-Notifications.md) | VAPID Web Push pipeline, background timer reminders, and service worker handler |
| 17 | [Classification Management](Classification-Management.md) | BGL, ketone, blood pressure, and pulse rate classification range management |
| 18 | [Cycles & Terms](Cycles-Terms.md) | Care cycle management and JWT term binding |
| 19 | [Feature & Bug Reports](Feature-Bug-Reports.md) | User-submitted feature requests and bug reports with admin review workflow |
| 20 | [App Configuration](App-Configuration.md) | Runtime key-value configuration store with secret masking and bulk update |

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

| Role ID | Role Name | Access Level |
|---|---|---|
| 1 | SuperAdmin | Full system access |
| 2 | Administrator | Full access except system-level config |
| 3 | Carer | Clinical entry for linked Care Recipients |
| 4 | SupportWorker | Clinical entry for linked Care Recipients |
| 5 | CareRecipient | Own data only |
| 6 | HealthCareProvider | Read access for linked Care Recipients |

---

## Wiki Options

> These documents can be published as a **GitHub Wiki**, **MkDocs site**, or **GitBook** — see notes below.

### GitHub Wiki

If this repository is hosted on GitHub, the built-in Wiki can host these files directly:

1. Enable the Wiki tab on the repository settings page.
2. Clone the wiki repo: `git clone https://github.com/CarolineChirenje/vitara.wiki.git`
3. Copy all `.md` files from this folder into the wiki repo root.
4. Rename `README.md` → `Home.md` (GitHub Wiki uses `Home.md` as the landing page).
5. Push and the wiki is live at `https://github.com/<org>/<repo>/wiki`.

**Limitation:** GitHub Wiki does not support subdirectories — all files must live in the root.

### MkDocs (Recommended for self-hosted)

Produces a fully searchable static site from these markdown files.

```yaml
# mkdocs.yml (place in project root)
site_name: Vitara Technical Docs
docs_dir: docs/technical documents
nav:
  - Home: README.md
  - Authentication: Authentication.md
  - Biometric Auth: Biometric-Authentication.md
  - BGL Assessment: BGL-Assessment.md
  - Blood Pressure: Blood-Pressure-Monitoring.md
  - Incidents (Diabetes): Incidents-Diabetes.md
  - Incidents (BP): Incidents-Blood-Pressure.md
  - Meal Entry: Meal-Entry.md
  - Dashboard: Dashboard.md
  - Profile: Profile.md
  - Care Recipients: Care-Recipients.md
  - Notifications: Notifications.md
  - Supplies: Supplies.md
  - Management: Management.md
  - Export & Reports: Export-Reports.md
  - Offline Queue: Offline-Queue.md
  - Push Notifications: Push-Notifications.md
  - Classification: Classification-Management.md
  - Cycles & Terms: Cycles-Terms.md
  - Feature & Bug Reports: Feature-Bug-Reports.md
  - App Configuration: App-Configuration.md
theme:
  name: material
```

```bash
pip install mkdocs mkdocs-material
mkdocs serve        # local preview at http://localhost:8000
mkdocs build        # outputs static site to /site
```

### GitBook

1. Create a free workspace at [gitbook.com](https://gitbook.com).
2. Connect it to the GitHub repository.
3. GitBook auto-imports all `.md` files from the connected docs folder.
4. A `SUMMARY.md` file controls the navigation structure (same format as this README table).
