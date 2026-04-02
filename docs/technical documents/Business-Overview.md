# Business Overview

## What is Vitara?

**Vitara** is a cloud-hosted, mobile-first health management platform purpose-built for the disability and aged care sector. It digitises the clinical workflows that support workers, carers, and healthcare providers perform every day — replacing paper forms, spreadsheets, and siloed apps with a single, secure, offline-capable Progressive Web App.

---

## The Problem We Solve

The disability and aged care industry manages the health of hundreds of thousands of vulnerable people. The current reality for most providers is:

| Pain Point | Impact |
|---|---|
| Paper-based incident logging | Data entry lag, illegible records, audit risk |
| Manual blood glucose & BP tracking | No early-warning signals, reactive care only |
| Disconnected meal / insulin records | No cross-referencing against incidents |
| No real-time carer alerts | Critical events go unnoticed for hours |
| Spreadsheet-based reporting to health providers | Error-prone, time-consuming, non-standardised |
| No supply visibility | Running out of consumables mid-cycle |

These gaps increase clinical risk, drive regulatory non-compliance, and add administrative burden to already stretched care teams.

---

## The Vitara Solution

Vitara connects **Care Recipients**, **Carers**, **Support Workers**, **Administrators**, and **Healthcare Providers** in one role-gated platform. Every clinical interaction is recorded, classified, and surfaced in real time.

### Core Capabilities

| Capability | What It Does |
|---|---|
| **Diabetes Incident Management** | Records hypo/hyper events, auto-classifies severity, fires push alerts to linked carers |
| **Blood Pressure Monitoring** | Multi-reading sessions with AHA-standard classification and incident logging |
| **BGL Assessment** | Guided state-machine assessment (symptoms → BGL reading → ketone check → intervention) with configurable severity thresholds |
| **Meal & Bolus Tracking** | Carbohydrate and insulin dose logging with full history and exportable bolus report |
| **Real-Time Push Notifications** | VAPID web push to mobile devices; background reminder timers for missed readings |
| **Excel Report Export** | One-click generation of Diabetes, BP, BGL, or All-Data reports scoped to a date range |
| **Google Drive Integration** | Automatic upload of generated reports to a `Vitara` folder in the Care Recipient's Google Drive |
| **Offline-First Architecture** | IndexedDB queue captures entries when offline; auto-syncs when connectivity returns |
| **Supply Tracking** | Consumable inventory with quantity projection and weekly low-stock email alerts |
| **Biometric / Passwordless Login** | WebAuthn/FIDO2 fingerprint and face-ID login — no password fatigue for field staff |
| **MFA (TOTP)** | Optional time-based one-time password second factor for administrator accounts |
| **Classification Management** | Administrators configure BGL, ketone, BP, and pulse rate severity ranges without a code deploy |

---

## Target Market

### Primary

- **Registered disability service providers**
- **Residential aged care providers** and **home care package managers**
- **Group homes** supporting adults with type 1 or type 2 diabetes and/or cardiovascular conditions

### Secondary

- **Individual families** coordinating care for a relative under a self-managed plan
- **Healthcare providers** (GPs, endocrinologists, cardiologists) who review data for multiple clients
---

## Competitive Advantage

| Dimension | Vitara | Generic Health Apps | Paper / Spreadsheets |
|---|---|---|---|
| Purpose-built for carer workflows | Yes | No | No |
| Multi-role, role-gated access | Yes | Rarely | No |
| Offline-first (works without internet) | Yes | Rarely | Yes (sort of) |
| Real-time push alerts to carers | Yes | Sometimes | No |
| Automated classification & escalation | Yes | No | No |
| Configurable severity thresholds | Yes | No | No |
| One-click Google Drive report delivery | Yes | No | No |
| Biometric login for field staff | Yes | Rarely | No |
| Aged care workflow alignment | Yes | No | No |

---

## Technology Foundation

Vitara is built on proven, enterprise-grade open standards:

| Layer | Technology | Why |
|---|---|---|
| Frontend | Angular 21 PWA | Installable on any device, offline-capable, single codebase |
| Backend | ASP.NET Core 10 (.NET 10) | High-performance, cross-platform, long-term Microsoft support |
| Database | PostgreSQL | Robust, open-source, HIPAA-aligned encryption at rest |
| Authentication | JWT + WebAuthn/FIDO2 + TOTP | Industry-leading identity standards |
| Push Notifications | VAPID Web Push | No proprietary notification vendor lock-in |
| Export | ClosedXML + Google Drive API v3 | Standards-based, works with the tools care providers already use |
| Hosting | Cloud-hosted API (elroitec.com) | Managed infrastructure, always up-to-date |

Zero dependency on proprietary mobile SDKs — Vitara runs in any modern browser, on any device, without an app store.

---

## Revenue Model

Vitara is positioned as a **SaaS subscription platform**:

| Tier | Target | Pricing Model |
|---|---|---|
| **Starter** | Individual families / small providers (1–5 Care Recipients) | Per Care Recipient / month |
| **Professional** | Mid-size providers (6–50 Care Recipients) | Per Care Recipient / month (volume discount) |
| **Enterprise** | Large providers (50+ Care Recipients) | Annual contract, white-label option |

Additional revenue opportunities:
- **Onboarding & training services**
- **Integration services** (connecting to portals, aged care management software)
- **Compliance reporting add-ons** (automated regulatory report packs)

---

## Regulatory & Compliance Alignment

| Framework | How Vitara Helps |
|---|---|
| Quality & Safeguards Commission | Timestamped incident records with classification and carer acknowledgement |
| Aged Care Quality Standards | Documented care interactions, supply tracking, healthcare provider access |
| My Health Record (future roadmap) | Structured export data compatible with HL7 FHIR mapping |
| Privacy Principals | JWT-scoped data access, role-based visibility, no data cross-contamination between providers |

---

## Traction & Validation

- Fully functional platform deployed to  `vitara.elroitec.com`
- Angular 21 PWA installable to phone home screen; service worker confirmed working
- Aligned clinical workflows to be validated against real support worker use cases
- 20 documented features across clinical, administrative, and infrastructure domains

---

## Investment Ask

Vitara is seeking investment to:

1. **Accelerate sales & onboarding** 
2. **Achieve compliance certification** — formal registration as a clinical software provider
3. **Build the integration layer** — API connectors to leading aged care platforms
4. **Expand the clinical module set** — wound care, medication management, and behaviour support plans

Vitara has the working product and the deep domain knowledge. Investment unlocks the distribution and certification that turns a strong technical foundation into a scalable business.

---

## Summary

> Vitara is the care coordination platform that disability and aged care providers need but don't yet have purpose-built workflows, real-time alerts, offline resilience, and clinical-grade data quality, delivered as a modern PWA that works on any device with no app store required.
