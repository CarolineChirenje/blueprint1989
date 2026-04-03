# Business Overview

## What is Divvy?

**Divvy** is a cloud-hosted, mobile-first expense-sharing application for households and groups. It replaces informal spreadsheets and manual IOUs with a structured, offline-capable Progressive Web App that tracks shared expenses, calculates member obligations, and guides payments.

---

## The Problem We Solve

Groups that share living costs — flatmates, families, travel groups, shared households — face a recurring challenge:

| Pain Point | Impact |
|---|---|
| Informal expense tracking (spreadsheets, notes, memory) | Disputes, forgotten debts, social friction |
| No structured repayment workflow | Obligations never clearly settled |
| No visibility into who owes what | Repeated manual calculations |
| Multiple disconnected payment apps | No single source of truth |
| No offline capability | Can't add expenses without internet |

These gaps lead to financial ambiguity and social tension within groups.

---

## The Divvy Solution

Divvy connects **Admins** and **Members** in one role-gated platform. Every expense is recorded, attributed, and surfaced as a concrete obligation.

### Core Capabilities

| Capability | What It Does |
|---|---|
| **Expense Cycle Management** | Define a billing period with a start and end date, add members, and track all shared expenses within that window |
| **Expense Tracking** | Record expenses by category (Rent, Utilities, Groceries, Transport, Entertainment, Other) with amount, description, and payer |
| **Obligation Calculation** | On cycle close, the system automatically calculates each member's net balance and generates the minimum set of payment transfers |
| **Payment Workflow** | Members submit payments against their obligations; Admins confirm or reject |
| **Real-Time Push Notifications** | VAPID web push notifies members on payment due, payment received, and cycle creation events |
| **Offline-First Architecture** | IndexedDB queue captures expense and payment entries when offline; auto-syncs on reconnect |
| **Biometric / Passwordless Login** | WebAuthn/FIDO2 fingerprint and Face ID login — fast access for everyday use |
| **MFA (TOTP)** | Optional TOTP second factor for Admin accounts |

---

## Target Market

### Primary

- **Shared households and flatmates** splitting rent, utilities, and groceries
- **Travel groups** tracking trip expenses and splitting costs
- **Family units** managing a shared household budget

### Secondary

- **Small businesses** managing shared team expenses
- **Event organisers** tracking and settling event costs across contributors

---

## Competitive Advantage

| Dimension | Divvy | Spreadsheets | Generic expense apps |
|---|---|---|---|
| Structured cycle-based periods | Yes | No | Rarely |
| Role-based access (Admin / Member) | Yes | No | No |
| Offline-first (works without internet) | Yes | Yes (sort of) | Rarely |
| Real-time push alerts | Yes | No | Sometimes |
| Automated obligation calculation | Yes | Manual | Sometimes |
| Biometric login | Yes | No | Rarely |
| PWA — no app store required | Yes | No | No |

---

## Technology Foundation

Divvy is built on proven, enterprise-grade open standards:

| Layer | Technology | Why |
|---|---|---|
| Frontend | Angular 21 PWA | Installable on any device, offline-capable, single codebase |
| Backend | ASP.NET Core 10 (.NET 10) | High-performance, cross-platform, long-term Microsoft support |
| Database | PostgreSQL | Robust, open-source |
| Authentication | JWT + WebAuthn/FIDO2 + TOTP | Industry-leading identity standards |
| Push Notifications | VAPID Web Push | No proprietary notification vendor lock-in |
| Hosting | Cloud-hosted API (elroitec.com) | Managed infrastructure, always up-to-date |

Zero dependency on proprietary mobile SDKs — Divvy runs in any modern browser, on any device, without an app store.

---

## Revenue Model

Divvy is positioned as a **SaaS subscription platform**:

| Tier | Target | Pricing Model |
|---|---|---|
| **Free** | Small groups (up to 5 members) | Free |
| **Pro** | Larger groups or multiple cycles | Per group / month |
| **Team** | Organisations managing multiple groups | Annual contract |

---

## Traction & Validation

- Fully functional platform deployed to `divvy.elroitec.com`
- Angular 21 PWA installable to phone home screen; service worker confirmed working
- Complete expense cycle workflow: create → add members → add expenses → close → obligations → payments

---

## Summary

> Divvy is the shared expense platform that households and groups need — structured billing cycles, automated obligation calculation, real-time alerts, and offline resilience — delivered as a modern PWA that works on any device with no app store required.
