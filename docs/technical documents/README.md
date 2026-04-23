# Blueprint1989 â€” Technical Documentation

This folder contains detailed technical documentation for every feature in the Blueprint1989 expense-sharing platform. Each document covers both the backend (.NET 10 API) and frontend (Angular 21 PWA) implementation for its respective feature.

---

## Table of Contents

| # | Document | Feature Summary |
|---|---|---|
| 1 | [Business Overview](Business-Overview.md) | Platform vision, target market, capabilities, and technology stack |
| 2 | [Authentication](Authentication.md) | Login, signup, MFA, password reset, JWT token lifecycle |
| 3 | [Biometric Authentication](Biometric-Authentication.md) | WebAuthn/FIDO2 passwordless login and credential management |
| 4 | [Dashboard](Dashboard.md) | Role-based navigation cards and time-of-day greeting |
| 5 | [Groups](Groups.md) | Group management, membership, invites, join-by-code, and role management |
| 6 | [Expense Cycles](Cycles.md) | Expense cycle management, member balances, close workflow |
| 7 | [Profile](Profile.md) | Profile sub-pages: name/password, MFA, biometric, devices, reports, notifications |
| 8 | [Notifications](Notifications.md) | In-app notification bell inbox, Web Push pipeline, and per-user preferences |
| 9 | [Management (Admin Hub)](Management.md) | Centralised admin hub: users, cycles, reports, app config |
| 10 | [Offline Queue](Offline-Queue.md) | IndexedDB offline queue with 24-hour TTL and auto-sync on reconnect |
| 11 | [Push Notifications](Push-Notifications.md) | VAPID Web Push pipeline and service worker handler |
| 12 | [Feature & Bug Reports](Feature-Bug-Reports.md) | User-submitted feature requests and bug reports with admin review workflow |
| 13 | [App Configuration](App-Configuration.md) | Runtime key-value configuration store with secret masking and bulk update |
| 14 | [Brand Identity](Brand-Identity.md) | Brand mark design, colour palette, typography, icon generation pipeline, and asset inventory |
| 15 | [KYC](KYC.md) | Identity verification gate for Mukando cycle participation |

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

### System Roles

| Role ID | Role Name | Description |
|---|---|---|
| 1 | SuperAdmin | Full system access – manage all users, app config, system restart, role definitions. Cannot self-register; must be created by an existing SuperAdmin. |
| 2 | Admin | Manage users, app config, reset passwords, verify emails. Can self-register with an admin PIN. |
| 3 | Member | Standard user – create/join groups, participate in cycles, manage own profile. |

### Group Roles

| Role ID | Role Name | Description |
|---|---|---|
| 1 | GroupAdmin | Invite/remove members, update member roles, manage join requests, regenerate join code, delete group. Automatically assigned to the group creator. Cannot leave if sole admin. |
| 2 | GroupMember | View group details, participate in cycles, leave group. |

### Authorization Policies

| Policy | Allowed Roles |
|---|---|
| SuperAdminOnly | SuperAdmin |
| AdminOrAbove | SuperAdmin, Admin |
| MemberOrAbove | SuperAdmin, Admin, Member (all authenticated users) |

### Signup Restrictions

| Role | Registration |
|---|---|
| SuperAdmin | Cannot self-register – must be created by the system |
| Admin | Self-register with admin PIN (configured in App Config) |
| Member | Self-register (no PIN required) |

### Permissions Summary

| Action | SuperAdmin | Admin | Member | GroupAdmin | GroupMember |
|---|:---:|:---:|:---:|:---:|:---:|
| Manage users | ✓ | ✓ | ✗ | ✗ | ✗ |
| Manage app config | ✓ | ✓ | ✗ | ✗ | ✗ |
| System restart | ✓ | ✗ | ✗ | ✗ | ✗ |
| Manage role definitions | ✓ | ✗ | ✗ | ✗ | ✗ |
| Create groups | ✓ | ✓ | ✓ | – | – |
| Invite/remove group members | ✓ | ✓ | ✗ | ✓ | ✗ |
| Manage cycles | ✓ | ✓ | ✗ | ✓ | ✗ |
| Join groups | ✓ | ✓ | ✓ | ✓ | ✓ |
| Participate in cycles | ✓ | ✓ | ✓ | ✓ | ✓ |
| Leave group | ✓ | ✓ | ✓ | ✓* | ✓ |

\* GroupAdmin cannot leave if they are the sole admin – must assign another admin first.

---

## Wiki Options

> These documents can be published as a **GitHub Wiki**, **MkDocs site**, or **GitBook** â€” see notes below.

### GitHub Wiki

If this repository is hosted on GitHub, the built-in Wiki can host these files directly:

1. Enable the Wiki tab on the repository settings page.
2. Clone the wiki repo: `git clone https://github.com/CarolineChirenje/Blueprint1989.wiki.git`
3. Copy all `.md` files from this folder into the wiki repo root.
4. Rename `README.md` â†’ `Home.md` (GitHub Wiki uses `Home.md` as the landing page).
5. Push and the wiki is live at `https://github.com/<org>/<repo>/wiki`.

**Limitation:** GitHub Wiki does not support subdirectories â€” all files must live in the root.

### MkDocs (Recommended for self-hosted)

Produces a fully searchable static site from these markdown files.

```yaml
# mkdocs.yml (place in project root)
site_name: Blueprint1989 Technical Docs
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
