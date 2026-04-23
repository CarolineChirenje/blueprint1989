# Blueprint1989

A cloud-hosted, mobile-first **Progressive Web App (PWA)** — a generic, extensible blueprint for building feature-rich web applications.

**Live:** [blueprint1989.elroitec.com](https://blueprint1989.elroitec.com) · **Docs:** [blueprint1989-docs.pages.dev](https://blueprint1989-docs.pages.dev)

## Features

- **Push Notifications** — real-time browser notifications via VAPID
- **Offline Support** — IndexedDB queue with 24-hour TTL, auto-syncs on reconnect
- **Biometric Login** — WebAuthn/FIDO2 passwordless authentication (fingerprint, Face ID)
- **MFA** — optional TOTP two-factor authentication with QR code setup
- **Admin Hub** — manage users, reports, and app-wide configuration
- **Role-Based Access** — SuperAdmin → Admin → Member

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Angular 21, Angular Material, TypeScript, RxJS, Chart.js |
| Backend | ASP.NET Core 10, C#, Entity Framework Core |
| Database | PostgreSQL (Npgsql) |
| Auth | JWT + WebAuthn/FIDO2 + TOTP MFA |
| Infrastructure | Ubuntu 22.04, Nginx, Kestrel, Cloudflare, Let's Encrypt |
| Secrets | Infisical SDK (production) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`10.0.103`)
- [Node.js](https://nodejs.org/) (LTS) + npm
- [PostgreSQL](https://www.postgresql.org/)
- `npm install -g http-server` (for local serving)
- [ngrok](https://ngrok.com/) (for HTTPS tunnelling during local testing)

## Getting Started

### 1. Clone and install

```bash
git clone <repo-url>
cd Blueprint1989

# Client
cd client
npm install

# Server
cd ../server/src/Blueprint1989.Api
dotnet restore
```

### 2. Configure environment

**Client** — edit `client/src/environments/environment.ts`:

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:1954/api',
  vapidPublicKey: '',
  version: '1.0.0',
  docsUrl: 'https://batanai-docs.pages.dev'
};
```

**Server** — uses `appsettings.Development.json` by default. Ensure PostgreSQL is running and the connection string matches your local setup.

### 3. Run locally (4 terminals)

```bash
# Terminal 1 — API
cd server/src/Blueprint1989.Api
dotnet run

# Terminal 2 — Build & serve client
cd client
npx ng build
http-server ./dist/Blueprint1989/browser -p 80 -c-1

# Terminal 3 — ngrok tunnel for client HTTPS
ngrok http 80

# Terminal 4 — Forward API port 5000 via VS Code Ports panel (set to Public)
```

> When tunnel URLs change, update CORS in `Program.cs` and `apiUrl` in `environment.ts`, then rebuild.

See [docs/local-testing-setup.md](docs/local-testing-setup.md) for full details.

## Project Structure

```
client/          Angular PWA frontend
server/          .NET backend API + VAPID key generator
docs/            Technical documentation & guides
scripts/         Icon generation utilities
```

## Documentation

Detailed docs live in the `docs/` folder and are published via MkDocs:

- [Architecture](docs/architecture.md)
- [Deployment Guide](docs/deployment-guide.md)
- [Push Notifications](docs/push-notifications.md)
- [Service Worker](docs/service-worker-quick-start.md)
- [Local Testing Setup](docs/local-testing-setup.md)