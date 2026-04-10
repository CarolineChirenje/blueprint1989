# Business Overview

## What is Batanai?

**Batanai** (Shona for "togetherness") is a cloud-hosted, mobile-first Progressive Web App for community-based financial management. It serves two core use cases:

1. **Majana â€” Shared Expense Splitting**: Groups split recurring living costs (rent, utilities, groceries) within structured billing cycles. The system tracks every expense, calculates each member's net balance, and generates the minimum set of payment transfers on cycle close.

2. **Mukando â€” Rotating Savings Groups**: Members contribute a fixed amount each round, and one member receives the full pool on a rotating basis. The platform manages contribution tracking, payout scheduling, proof of payment, and automated reminders.

Batanai replaces informal spreadsheets, WhatsApp threads, and manual IOUs with a single structured platform that works offline and on any device.

---

## The Problem We Solve

Communities that manage shared money â€” whether splitting bills or running rotating savings â€” face persistent challenges:

| Pain Point | Impact |
|---|---|
| Informal tracking (spreadsheets, notes, memory) | Disputes, forgotten debts, social friction |
| No structured repayment or contribution workflow | Obligations never clearly settled |
| No visibility into who owes what or who has contributed | Repeated manual calculations |
| No accountability in rotating savings (stokvels/mukandos) | Missed contributions, unfair payouts |
| No offline capability | Can't record transactions without internet |
| Multiple disconnected messaging and payment apps | No single source of truth |

These gaps lead to financial ambiguity, broken trust, and the collapse of community savings initiatives.

---

## The Batanai Solution

Batanai organises users into **Groups**, each managed by one or more **Group Admins**. Within a group, admins create **Expense Cycles** of either type â€” Majana or Mukando. Every financial event is recorded, attributed, and surfaced as a concrete obligation or contribution.

### Core Capabilities

| Capability | What It Does |
|---|---|
| **Group Management** | Create groups, invite members by email or shareable join code (with QR), manage roles (Admin / Member), and approve join requests |
| **Majana Expense Cycles** | Define a billing period with start/end dates, record expenses by category (Rent, Utilities, Groceries, Transport, Entertainment, Other), and automatically calculate obligations on close |
| **Mukando Rotating Savings** | Configure contribution amount, frequency (Weekly / Biweekly / Monthly), and payout order; system auto-generates rounds, tracks contributions, records payouts with proof, and sends reminders |
| **Obligation & Balance Calculation** | On Majana cycle close, the system computes each member's net balance and produces the minimum transfer set. For Mukando, each round's expected pool and contribution status are tracked in real time |
| **Payment Workflow** | Members submit payments (Majana) or contributions (Mukando) with optional proof; Admins confirm or reject |
| **Real-Time Push Notifications** | VAPID web push alerts for cycle events, payment due dates, contribution reminders, join requests, KYC verification, and more |
| **Duration-Agnostic Reminder Engine** | DB-backed, event-driven reminder scheduling that adapts to any cycle length (1 day to 1 year). Payment reminders are created when a cycle starts; KYC reminders are created when an unverified member is added to a Mukando cycle. All milestones are configurable via the admin AppConfig UI. A lightweight dispatcher sends due reminders with eligibility rechecks, retry logic, and automatic cancellation when obligations are settled or KYC is completed |
| **Offline-First Architecture** | IndexedDB queue captures expense, payment, and contribution entries when offline; auto-syncs on reconnect with 24-hour TTL |
| **Biometric / Passwordless Login** | WebAuthn/FIDO2 fingerprint and Face ID login for fast everyday access |
| **TOTP Multi-Factor Authentication** | Optional TOTP second factor for added account security |
| **Opt-Out & Swap Requests (Mukando)** | Members can request to opt out (with reason) or swap payout rounds; admins approve or decline |
| **Admin Dashboard & Management Hub** | Centralised console for user management, cycle oversight, app configuration, and reports |

---

## Target Market

### Primary

- **Shared households and flatmates** splitting rent, utilities, and groceries (Majana)
- **Rotating savings groups (stokvels / mukandos)** pooling contributions monthly (Mukando)
- **Family units** managing a shared household budget

### Secondary

- **Travel groups** tracking and settling trip expenses
- **Small businesses and teams** managing shared operational costs
- **Event organisers** tracking contributions and settling costs across participants
- **Church and community groups** running fundraising or savings rounds

---

## Competitive Advantage

| Dimension | Batanai | Spreadsheets | Generic expense apps | Stokvel apps |
|---|---|---|---|---|
| Structured cycle-based periods | âœ“ | âœ— | Rarely | Sometimes |
| Rotating savings (Mukando) support | âœ“ | âœ— | âœ— | âœ“ |
| Role-based access (Group Admin / Member) | âœ“ | âœ— | âœ— | Rarely |
| Join-by-code with QR sharing | âœ“ | âœ— | âœ— | Rarely |
| Offline-first (works without internet) | âœ“ | Partially | Rarely | âœ— |
| Real-time push alerts & reminders | âœ“ | âœ— | Sometimes | Sometimes |
| Automated obligation calculation | âœ“ | Manual | Sometimes | âœ— |
| Biometric login (WebAuthn/FIDO2) | âœ“ | âœ— | Rarely | âœ— |
| PWA â€” no app store required | âœ“ | âœ— | âœ— | âœ— |
| Contribution proof & payment tracking | âœ“ | âœ— | âœ— | Sometimes |

---

## How It Works

### Majana Flow (Expense Sharing)

```
Create Group â†’ Invite Members â†’ Create Majana Cycle (start/end dates)
    â†' Members Add Expenses â†' Admin Starts Cycle
    â†' System Calculates Obligations & Schedules Payment Reminders
    â†' Members Pay â†' Admin Confirms â†' Reminders Auto-Cancel on Settlement
    â†' Admin Closes Cycle â†' All Remaining Reminders Cancelled
```

### Mukando Flow (Rotating Savings)

```
Create Group â†' Invite Members â†' Create Mukando Cycle (Draft)
    â†' Add Members (KYC reminders auto-scheduled for unverified members)
    â†' Configure: Amount, Frequency, Payout Order â†' All Members Verify KYC
    â†' Activate Cycle (KYC reminders cancelled, payment reminders scheduled)
    â†’ Round 1: All contribute â†’ Recipient receives pool â†’ Admin records payout
    â†’ Round 2: Next recipient â†’ ... â†’ All rounds complete â†’ Cycle closed
```

---

## Technology Foundation

Batanai is built on proven, enterprise-grade open standards:

| Layer | Technology | Why |
|---|---|---|
| Frontend | Angular 21 PWA | Installable on any device, offline-capable, single codebase |
| Backend | ASP.NET Core 10 (.NET 10) | High-performance, cross-platform, long-term Microsoft support |
| Database | PostgreSQL 15+ (EF Core 10) | Robust, open-source relational database |
| Authentication | JWT + WebAuthn/FIDO2 + TOTP | Industry-leading identity standards |
| Push Notifications | VAPID Web Push | No proprietary notification vendor lock-in |
| Offline Support | IndexedDB + Service Worker | Full offline queue with automatic sync |
| Hosting | Ubuntu 22.04 VPS, Nginx, Let's Encrypt TLS | Managed infrastructure at `batanai.elroitec.com` |

Zero dependency on proprietary mobile SDKs â€” Batanai runs in any modern browser, on any device, without an app store.

---

## Platform Roles

### System Roles

| Role | Access Level |
|---|---|
| **SuperAdmin** | Full system access â€” users, cycles, configuration, all groups |
| **Admin** | Manage users, cycles, app config |
| **Member** | Participate in groups and cycles |

### Group Roles

| Role | Access Level |
|---|---|
| **Group Admin** | Create cycles, invite/remove members, approve join requests, manage group settings |
| **Group Member** | View group, participate in cycles, submit expenses/contributions |

---

## Revenue Model

Batanai is positioned as a **SaaS subscription platform**:

| Tier | Target | Pricing Model |
|---|---|---|
| **Free** | Small groups (up to 5 members) | Free forever |
| **Pro** | Larger groups or multiple active cycles | Per group / month |
| **Team** | Organisations managing multiple groups | Annual contract |

---

## Traction & Validation

- Fully functional platform deployed to `batanai.elroitec.com`
- Angular 21 PWA installable to phone home screens; service worker confirmed working
- Complete Majana workflow: create â†’ add members â†’ add expenses â†’ close â†’ obligations â†’ payments
- Complete Mukando workflow: create â†’ configure â†’ activate â†’ contributions â†’ payouts â†’ close
- Group management with invite-by-email, join-by-code (QR), and admin approval workflows
- Push notifications operational across all major browsers
- Offline queue tested and confirmed with auto-sync on reconnect

---

## Summary

> Batanai is the community finance platform that brings structure, transparency, and trust to shared expenses and rotating savings â€” with structured billing cycles, automated obligation calculation, real-time alerts, offline resilience, and biometric authentication â€” delivered as a modern PWA that works on any device with no app store required.
