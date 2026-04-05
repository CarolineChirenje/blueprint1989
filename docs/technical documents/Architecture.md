# Batanai — System Architecture

## Overview

Batanai is a Progressive Web Application (PWA) for shared expense management, built with an Angular 21 frontend and a .NET 10 REST API backend, backed by PostgreSQL and hosted on Ubuntu 22.04.

---

## System Architecture Diagram

```mermaid
graph TB
    subgraph Users["👤 Users"]
        U1[Admin / Member\nBrowser / PWA]
    end

    subgraph DNS["🌐 DNS"]
        NS[Namesilo\nelroitec.com zone]
    end

    subgraph CF["☁️ Cloudflare"]
        CFCDN[Cloudflare CDN\nDDoS + WAF]
        CFPAGES[Cloudflare Pages\nbatanai-docs.pages.dev]
        CFZT[Cloudflare Zero Trust\nOptional SSO gate on docs]
    end

    subgraph Server["🖥️ Ubuntu 22.04 VPS"]
        NGINX[Nginx Reverse Proxy\nTLS — Let's Encrypt]

        subgraph Frontend["Angular 21 PWA — batanai.elroitec.com"]
            FE_PWA[PWA Shell\nService Worker + Offline Queue]
            FE_AUTH[Auth Module\nLogin · Signup · MFA · Biometric]
            FE_FEAT[Feature Modules\nDashboard · Cycles · Expenses\nPayments · Profile · Management]
            FE_CORE[Core Services\nHTTP Interceptor · Push · Sync\nNotifications]
        end

        subgraph API[".NET 10 API — batanaiapi.elroitec.com"]
            KESTREL[Kestrel :5000]
            CTRL[REST Controllers\nAuth · ExpenseCycle · Expense\nPayment · Notifications · Push]
            SVC[Domain Services\nExpenseCycle · Expense · Payment\nPush · MFA · WebAuthn\nEmail · AppConfig]
            end

        subgraph DB["Database"]
            PG[(PostgreSQL 15+\nEF Core 10 — snake_case\nlocalhost only)]
        end
    end

    subgraph ExtServices["🔌 External Services"]
        INFISICAL[Infisical\nSecrets Manager\nSDK v3.0.4]
        VAPID[Web Push — VAPID\nLib.Net.Http.WebPush\nBrowser push notifications]
        EMAIL[Email Service\nPassword reset\nNotifications]
    end

    subgraph Docs["📚 Documentation"]
        MKDOCS[MkDocs + Material\nCloudflare Pages CDN]
    end

    %% User flows
    U1 -->|HTTPS| NS
    NS -->|DNS resolution| CFCDN
    CFCDN -->|Proxied HTTPS| NGINX

    %% Nginx routing
    NGINX -->|Static files| FE_PWA
    NGINX -->|Reverse proxy| KESTREL

    %% Frontend internal
    FE_PWA --> FE_AUTH
    FE_PWA --> FE_FEAT
    FE_PWA --> FE_CORE

    %% API internal
    KESTREL --> CTRL
    CTRL --> SVC
    SVC --> PG

    %% Auth mechanisms
    CTRL -->|JWT Bearer + TOTP MFA\nWebAuthn FIDO2 Biometric| FE_AUTH

    %% External service connections
    API -->|Fetch secrets at startup\nDB conn · JWT keys · VAPID keys\nWebAuthn config| INFISICAL
    SVC -->|VAPID push dispatch\nPer user-device subscription| VAPID
    SVC -->|SMTP / transactional email| EMAIL

    %% Docs
    MKDOCS -.->|Deployed via GitHub| CFPAGES
    CFZT -.->|Access policy| CFPAGES

    %% Push to browser
    VAPID -->|Push notification| U1
```

---

## Component Breakdown

### Frontend — Angular 21 PWA

| Layer | Detail |
|---|---|
| **Framework** | Angular 21.2.5, TypeScript ~5.9.3 |
| **UI Library** | Angular Material + CDK 21.2.3 |
| **Charts** | Chart.js 4.5.1 |
| **Excel** | xlsx 0.18.5 (client-side report generation) |
| **PWA** | Custom service worker (`custom-sw.js`), `ngsw-config.json`, `manifest.webmanifest` |
| **Offline** | Offline queue service — defers mutations when offline |
| **Auth** | JWT interceptor (`AuthInterceptor`), TOTP MFA, WebAuthn biometric |
| **Hosting** | Nginx serving static build at `batanai.elroitec.com` |

**Feature Modules**

| Module | Functionality |
|---|---|
| `expense-cycle` | Expense cycle management, member balances, close workflow |
| `expense` | Expense entry and history within a cycle |
| `payment` | Payment submission, confirmation, and history |
| `dashboard` | Summary cards, quick navigation |
| `offline-queue` | View and manage queued offline actions |
| `profile` | User profile, MFA, biometric, notifications, devices |
| `management` | Admin hub: users, cycles, reports, app config |
| `report-feature-bug` | In-app bug and feature reporting |
| `release-notes` | App changelog |
| `push-test` | Push notification testing (dev/admin) |
| `about` | App information |

---

### Backend — .NET 10 API

| Layer | Detail |
|---|---|
| **Framework** | ASP.NET Core 10.0 / Kestrel on port 5000 |
| **ORM** | EF Core 10 + Npgsql 10.0.0 (snake_case naming) |
| **Auth** | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`), two keys: standard + MFA temp |
| **MFA** | TOTP via `MfaService` + `QRCoder` v1.7.0 |
| **Biometric** | FIDO2/WebAuthn via `Fido2NetLib` v3.0.1 |
| **Documentation** | Swagger / OpenAPI via `Swashbuckle.AspNetCore` v10.1.4 |
| **Hosting** | Nginx reverse proxy → Kestrel at `batanaiapi.elroitec.com` |

**Controller Groups**

| Domain | Controllers |
|---|---|
| Auth / Users | `AuthController`, `WebAuthnController`, `RoleController`, `UserDeviceController` |
| Expenses | `ExpenseCycleController`, `ExpenseController`, `PaymentController` |
| Admin | `AppConfigController`, `FeatureBugReportController`, `SystemController` |
| Notifications | `NotificationController`, `NotificationPreferenceController`, `PushController` |

---

### Database — PostgreSQL 15+

| Detail | Value |
|---|---|
| **Engine** | PostgreSQL 15+ |
| **ORM** | EF Core 10 with `UseSnakeCaseNamingConvention()` |
| **Connection** | `localhost` only; remote access via SSH tunnel |
| **Retry policy** | `EnableRetryOnFailure(maxRetry: 5, delay: 30s)` |
| **Key tables** | `users`, expense_cycles, cycle_members, expenses, payments, member_obligations, notifications, user_devices, webauthn_credentials, app_config, notification_type, user_notification_preferences |

---

### External Services

#### Infisical — Secrets Management

| Detail | Value |
|---|---|
| **SDK** | `Infisical.Sdk` v3.0.4 |
| **Auth method** | Machine Identity / Universal Auth |
| **Environment slug** | `prod` |
| **Secrets managed** | PostgreSQL connection string, `Jwt:Key`, `Jwt:MfaTempKey`, `Vapid:Subject/PublicKey/PrivateKey`, `WebAuthn:RelyingPartyId/Origin` |
| **Dev alternative** | `dotnet user-secrets` (never committed) |

#### Google Drive API v3

Not used in Batanai.

#### Web Push (VAPID)

| Detail | Value |
|---|---|
| **Library** | `Lib.Net.Http.WebPush` v3.3.1 |
| **Keys** | VAPID public/private keys managed by Infisical |
| **Subscriptions** | Stored per user-device in PostgreSQL |
| **Trigger** | `PushNotificationSender` dispatches on payment events and cycle creation |
| **Browser** | Delivered via browser push API to PWA service worker |

#### Cloudflare

| Detail | Value |
|---|---|
| **CDN / WAF** | Cloudflare proxies all traffic to the VPS — DDoS protection, caching |
| **DNS** | Domain registered at Namesilo; Cloudflare nameservers manage `elroitec.com` |
| **Pages** | Hosts MkDocs documentation site at `batanai-docs.pages.dev` |
| **Zero Trust** | Optional Google Workspace / GitHub IdP SSO gate on the docs site |

---

### Authentication Flows

```mermaid
sequenceDiagram
    participant B as Browser (Angular)
    participant API as .NET 10 API
    participant DB as PostgreSQL

    %% Standard login
    B->>API: POST /auth/login (email + password)
    API->>DB: Verify password hash
    alt MFA enabled
        API-->>B: MFA challenge token (short-lived JWT)
        B->>API: POST /auth/mfa/verify (TOTP code)
        API-->>B: Full access JWT
    else No MFA
        API-->>B: Full access JWT
    end

    %% Biometric login
    B->>API: POST /webauthn/assertion/options
    API-->>B: FIDO2 assertion challenge
    B->>B: navigator.credentials.get() — TouchID/FaceID/PIN
    B->>API: POST /webauthn/assertion/complete
    API-->>B: Full access JWT

    %% All subsequent calls
    B->>API: Any request + Authorization: Bearer <JWT>
    API->>API: Validate JWT (JwtBearer middleware)
```

---

### Notification Flow

```mermaid
flowchart LR
    A[Batanai event\ne.g. Payment due / Cycle created] --> B[Domain Service\nExpenseCycleService etc.]
    B --> C{Check UserNotificationPreference}
    C -->|IsEnabled = true| D[PushNotificationSender]
    C -->|IsEnabled = false| E[In-app bell only]
    D --> F[VAPID push via\nLib.Net.Http.WebPush]
    F --> G[Browser Push API]
    G --> H[custom-sw.js\nService Worker]
    H --> I[Push notification\ndisplayed to user]
    D --> E
```

---

### Secrets Management Flow

```mermaid
flowchart TD
    A[API startup\nProgram.cs] --> B{Environment}
    B -->|Development| C[dotnet user-secrets\n+ appsettings.Development.json]
    B -->|Production| D[Infisical SDK\nMachine Identity Auth]
    D --> E[Infisical Cloud\nProject: prod env]
    E -->|Inject at runtime| F[IConfiguration\nJwt · DB · VAPID · WebAuthn]
    C --> F
    F --> G[ASP.NET Core services\nregistered with resolved config]
```

---

### Deployment Stack

```
                          Namesilo DNS (elroitec.com)
                                    │
                          Cloudflare Nameservers
                          CDN · WAF · DDoS protection
                                    │
                            Ubuntu 22.04 VPS
                            ┌───────────────┐
                            │     Nginx     │  ← TLS termination (Let's Encrypt)
                            └───┬───────┬───┘
                                │       │
                    ┌───────────┘       └──────────────┐
                    │                                  │
          batanai.elroitec.com              batanaiapi.elroitec.com
          Angular 21 static build         Kestrel :5000 (.NET 10)
          /var/www/Batanai                 systemd service
                                               │
                                         PostgreSQL 15
                                         (localhost:5432)
                                               │
                                      EF Core 10 migrations
```

---

*Last updated: March 2026*
