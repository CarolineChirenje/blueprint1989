# Business Overview

## What is Batanai?

**Batanai** (Shona for "togetherness") is a cloud-hosted, mobile-first Progressive Web App for community-based financial management. It serves two core use cases:

1. **Majana — Shared Expense Splitting**: Groups split recurring living costs (rent, utilities, groceries) within structured billing cycles. The system tracks every expense, calculates each member's net balance, and generates the minimum set of payment transfers on cycle close.

2. **Mukando — Rotating Savings Groups**: Members contribute a fixed amount each round, and one member receives the full pool on a rotating basis. The platform manages contribution tracking, payout scheduling, proof of payment, and automated reminders.

Batanai replaces informal spreadsheets, WhatsApp threads, and manual IOUs with a single structured platform that works offline and on any device.

---

## The Problem We Solve

Communities that manage shared money — whether splitting bills or running rotating savings — face persistent challenges:

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

Batanai organises users into **Groups**, each managed by one or more **Group Admins**. Within a group, admins create **Expense Cycles** of either type — Majana or Mukando. Every financial event is recorded, attributed, and surfaced as a concrete obligation or contribution.

### Core Capabilities

| Capability | What It Does |
|---|---|
| **Group Management** | Create groups, invite members by email or shareable join code (with QR), manage roles (Admin / Member), and approve join requests |
| **Majana Expense Cycles** | Define a billing period with start/end dates, record expenses by category (Rent, Utilities, Groceries, Transport, Entertainment, Other), and automatically calculate obligations on close |
| **Mukando Rotating Savings** | Configure contribution amount, frequency (Weekly / Biweekly / Monthly), and payout order; system auto-generates rounds, tracks contributions, records payouts with proof, and sends reminders |
| **Obligation & Balance Calculation** | On Majana cycle close, the system computes each member's net balance and produces the minimum transfer set. For Mukando, each round's expected pool and contribution status are tracked in real time |
| **Payment Workflow** | Members submit payments (Majana) or contributions (Mukando) with optional proof; Admins confirm or reject |
| **Real-Time Push Notifications** | VAPID web push alerts for cycle events, payment due dates, contribution reminders, join requests, and more |
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
| Structured cycle-based periods | ✓ | ✗ | Rarely | Sometimes |
| Rotating savings (Mukando) support | ✓ | ✗ | ✗ | ✓ |
| Role-based access (Group Admin / Member) | ✓ | ✗ | ✗ | Rarely |
| Join-by-code with QR sharing | ✓ | ✗ | ✗ | Rarely |
| Offline-first (works without internet) | ✓ | Partially | Rarely | ✗ |
| Real-time push alerts & reminders | ✓ | ✗ | Sometimes | Sometimes |
| Automated obligation calculation | ✓ | Manual | Sometimes | ✗ |
| Biometric login (WebAuthn/FIDO2) | ✓ | ✗ | Rarely | ✗ |
| PWA — no app store required | ✓ | ✗ | ✗ | ✗ |
| Contribution proof & payment tracking | ✓ | ✗ | ✗ | Sometimes |

---

## How It Works

### Majana Flow (Expense Sharing)

```
Create Group → Invite Members → Create Majana Cycle (start/end dates)
    → Members Add Expenses → Admin Closes Cycle
    → System Calculates Obligations → Members Pay → Admin Confirms
```

### Mukando Flow (Rotating Savings)

```
Create Group → Invite Members → Create Mukando Cycle (Draft)
    → Configure: Amount, Frequency, Payout Order → Activate Cycle
    → Round 1: All contribute → Recipient receives pool → Admin records payout
    → Round 2: Next recipient → ... → All rounds complete → Cycle closed
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

Zero dependency on proprietary mobile SDKs — Batanai runs in any modern browser, on any device, without an app store.

---

## Platform Roles

### System Roles

| Role | Access Level |
|---|---|
| **SuperAdmin** | Full system access — users, cycles, configuration, all groups |
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
- Complete Majana workflow: create → add members → add expenses → close → obligations → payments
- Complete Mukando workflow: create → configure → activate → contributions → payouts → close
- Group management with invite-by-email, join-by-code (QR), and admin approval workflows
- Push notifications operational across all major browsers
- Offline queue tested and confirmed with auto-sync on reconnect

---

## Summary

> Batanai is the community finance platform that brings structure, transparency, and trust to shared expenses and rotating savings — with structured billing cycles, automated obligation calculation, real-time alerts, offline resilience, and biometric authentication — delivered as a modern PWA that works on any device with no app store required.
