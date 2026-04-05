# Deploying the Batanai API with Infisical on Ubuntu

This guide covers setting up the Batanai API on an Ubuntu server using Infisical to manage secrets. The API runs as a systemd service under .NET 10, with secrets (DB connection string, JWT keys, Vapid keys, WebAuthn config) fetched from Infisical at startup.

---

## Prerequisites

- Ubuntu 22.04+ server
- PostgreSQL installed and running
- .NET 10 SDK/Runtime installed
- A published build of `Batanai.Api`
- Infisical project with secrets configured (environment slug: `prod`)
- Infisical Machine Identity created with Universal Auth (see [Machine Identity setup](#machine-identity-setup))

---

## Running Locally (Development)

In development, Infisical is **not used**. Secrets are stored in `dotnet user-secrets`, which are never committed to source control. The `ASPNETCORE_ENVIRONMENT` must be `Development` (the default when running with `dotnet run`).

### Prerequisites (both platforms)

- .NET 10 SDK installed
- PostgreSQL running locally
- A copy of `appsettings.Development.json` populated from the template (or user-secrets set individually — see below)

---

### Windows

**1. Install .NET 10 SDK**

Download and run the installer from [https://dot.net](https://dot.net/download) or use winget:

```powershell
winget install Microsoft.DotNet.SDK.10
```

**2. Navigate to the API project**

```powershell
cd C:\DEV\Batanai\server\src\Batanai.Api
```

**3. Set secrets using `dotnet user-secrets`**

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=Batanai;Username=postgres;Password=YOUR_PG_PASSWORD"
dotnet user-secrets set "Jwt:Key" "YOUR_JWT_KEY"
dotnet user-secrets set "Jwt:MfaTempKey" "YOUR_MFA_TEMP_KEY"
dotnet user-secrets set "Vapid:Subject" "mailto:admin@elroitec.com"
dotnet user-secrets set "Vapid:PublicKey" "YOUR_VAPID_PUBLIC_KEY"
dotnet user-secrets set "Vapid:PrivateKey" "YOUR_VAPID_PRIVATE_KEY"
dotnet user-secrets set "WebAuthn:RelyingPartyId" "localhost"
dotnet user-secrets set "WebAuthn:Origin" "http://localhost:4200"
```

> Tip: You can also copy `appsettings.Development.template.json` to `appsettings.Development.json` and fill in the values — that file is gitignored and loaded automatically in the Development environment.

**4. Run the API**

```powershell
dotnet run
```

The API starts at `https://localhost:7xxx` / `http://localhost:5xxx` (ports shown in the console output).

**5. List or clear secrets**

```powershell
# List all stored secrets
dotnet user-secrets list

# Remove a specific secret
dotnet user-secrets remove "Jwt:Key"

# Clear all secrets
dotnet user-secrets clear
```

---

### Linux (local development machine)

**1. Install .NET 10 SDK**

```bash
# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
```

**2. Navigate to the API project**

```bash
cd ~/DEV/Batanai/server/src/Batanai.Api
```

**3. Set secrets using `dotnet user-secrets`**

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=Batanai;Username=postgres;Password=YOUR_PG_PASSWORD"
dotnet user-secrets set "Jwt:Key" "YOUR_JWT_KEY"
dotnet user-secrets set "Jwt:MfaTempKey" "YOUR_MFA_TEMP_KEY"
dotnet user-secrets set "Vapid:Subject" "mailto:admin@elroitec.com"
dotnet user-secrets set "Vapid:PublicKey" "YOUR_VAPID_PUBLIC_KEY"
dotnet user-secrets set "Vapid:PrivateKey" "YOUR_VAPID_PRIVATE_KEY"
dotnet user-secrets set "WebAuthn:RelyingPartyId" "localhost"
dotnet user-secrets set "WebAuthn:Origin" "http://localhost:4200"
```

Secrets are stored in `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json` — never inside the project directory.

**4. Run the API**

```bash
dotnet run
```

**5. List or clear secrets**

```bash
# List all stored secrets
dotnet user-secrets list

# Remove a specific secret
dotnet user-secrets remove "Jwt:Key"

# Clear all secrets
dotnet user-secrets clear
```

---

### How Local Dev Differs from Production

| | Development (local) | Production (Ubuntu server) |
|---|---|---|
| Infisical used? | No | Yes |
| Secret source | `dotnet user-secrets` | Infisical `prod` environment |
| Credentials stored in | User secrets store | `appsettings.Production.json` (ClientId) + systemd `Environment=` (ClientSecret only) |
| `ASPNETCORE_ENVIRONMENT` | `Development` (default) | `Production` |
| `appsettings.Development.json` | Gitignored, optional | Not present on server |

---

## 1. Install .NET 10 Runtime

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install the ASP.NET Core runtime
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10.0
```

Verify:
```bash
dotnet --version
```

---

## 2. Create the App User and Directory

```bash
# Create a dedicated non-root user to run the service
sudo useradd -m -s /bin/bash Batanai

# Create the deployment directory
sudo mkdir -p /var/www/Batanai-api
sudo chown Batanai:Batanai /var/www/Batanai-api
```

---

## 3. Publish and Deploy the API

On your **development machine**, publish a production build:

```bash
cd server/src/Batanai.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
```

Copy the output to the server (replace `your-server-ip`):

```bash
scp -r ./publish/* user@your-server-ip:/var/www/Batanai-api/
```

---

## 4. Configure `appsettings.json` on the Server

The committed `appsettings.json` and `appsettings.Production.json` already contain all non-secret config and are part of the published output — no changes needed on the server.

Key values already committed:

| File | Key | Value |
|---|---|---|
| `appsettings.json` | `Infisical:ProjectId` | `c542b15c-1f74-4194-a759-e92153135201` |
| `appsettings.json` | `Infisical:EnvironmentSlug` | `prod` |
| `appsettings.Production.json` | `Infisical:ClientId` | `df238a64-86b5-439d-a3a0-4bed86065119` |

> **`ClientId` is an identifier, not a credential.** It cannot authenticate on its own. Only `ClientSecret` must be kept out of source control — it is injected via the systemd unit file.

---

## 5. Machine Identity Setup

In the Infisical dashboard:

1. Go to your project → **Access Control** → **Machine Identities**
2. Click **Create Identity** → name it `Batanai-api-production` → Auth method: **Universal Auth**
3. Click **Create**
4. On the identity page → **Client Secrets** tab → **Generate Client Secret**
5. Copy the **Client ID** (always visible) and **Client Secret** (shown once only)
6. Go to **Project Access** tab → **Add Project** → select the Batanai project → select **Production** environment → role: **Viewer**

---

## 6. Create the systemd Service Unit

```bash
sudo nano /etc/systemd/system/Batanai-api.service
```

Paste the following (replace credential values):

```ini
[Unit]
Description=Batanai API
After=network.target postgresql.service

[Service]
Type=notify
User=Batanai
WorkingDirectory=/var/www/Batanai-api
ExecStart=/usr/bin/dotnet /var/www/Batanai-api/Batanai.Api.dll

# ASP.NET Core environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000

# Infisical Machine Identity credential
# ClientId is in appsettings.Production.json — only the secret goes here
Environment=Infisical__ClientSecret=YOUR_CLIENT_SECRET

Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=Batanai-api

[Install]
WantedBy=multi-user.target
```

Secure the unit file (it contains the `ClientSecret`):

```bash
sudo chmod 600 /etc/systemd/system/Batanai-api.service
sudo chown root:root /etc/systemd/system/Batanai-api.service
```

---

## 7. Enable and Start the Service

```bash
# Reload systemd to pick up the new unit file
sudo systemctl daemon-reload

# Enable the service to start on boot
sudo systemctl enable Batanai-api

# Start the service
sudo systemctl start Batanai-api

# Check status
sudo systemctl status Batanai-api
```

---

## 8. Verify Infisical Secrets are Loading

Check the startup logs:

```bash
sudo journalctl -u Batanai-api -n 50 --no-pager
```

You should see output like:

```
[Infisical] Secret 'ConnectionStrings:DefaultConnection' length: raw=72 clean=72
[Infisical] Secret 'Jwt:Key' length: raw=70 clean=70
...
[Infisical] Loaded 8 secrets from environment 'prod'.
```

If you see `[Infisical] Infisical:ClientId not set — falling back to appsettings.`, the `Environment=` lines in the systemd unit are not being read — double-check for typos and run `sudo systemctl daemon-reload` again.

---

## 9. Set Up a Reverse Proxy with Nginx

```bash
sudo apt-get install -y nginx
sudo nano /etc/nginx/sites-available/Batanai-api
```

```nginx
server {
    listen 80;
    server_name batanaiapi.elroitec.com;

    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/Batanai-api /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## 10. HTTPS with Certbot (Let's Encrypt)

```bash
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d batanaiapi.elroitec.com
```

Certbot automatically renews — verify the renewal timer:

```bash
sudo systemctl status certbot.timer
```

---

## 11. Updating the Application

```bash
# On dev machine: publish new build
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish

# Copy to server
scp -r ./publish/* user@your-server-ip:/var/www/Batanai-api/

# On server: restart the service
sudo systemctl restart Batanai-api

# Watch logs to confirm clean startup
sudo journalctl -u Batanai-api -f
```

---

## 12. Rotating the Infisical Client Secret

If the client secret is compromised or expired:

1. In Infisical → Machine Identity → **Client Secrets** → revoke the old secret → generate a new one
2. On the server, update the systemd unit:
   ```bash
   sudo nano /etc/systemd/system/Batanai-api.service
   # Update Environment=Infisical__ClientSecret=NEW_SECRET
   sudo systemctl daemon-reload
   sudo systemctl restart Batanai-api
   ```

No code changes or redeployment needed.

---

## 13. Adding a New Secret

Follow these steps whenever you need to expose a new configuration value through Infisical.

### Naming Convention

ASP.NET Core uses `:` as the hierarchy separator in `IConfiguration` (e.g. `Jwt:Key`). Because `:` is not valid in most shell/environment variable names, Infisical secrets use **double-underscore `__`** as the separator. The provider maps `__` → `:` automatically at load time.

| Config key | Infisical secret name |
|---|---|
| `Jwt:Key` | `Jwt__Key` |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| `MyService:ApiKey` | `MyService__ApiKey` |
| `MyService:Nested:Value` | `MyService__Nested__Value` |

Rules:
- Use **PascalCase** to match the casing of the key in `appsettings.json`
- Replace every `:` with `__` (double underscore)
- No spaces, no special characters other than `__`

### Step-by-Step

**1. Add the secret in Infisical**

1. Open [app.infisical.com](https://app.infisical.com) → your project → **Secrets** → select the **prod** environment
2. Click **Add Secret**
3. Enter the secret name using the `__` convention (e.g. `MyService__ApiKey`)
4. Enter the secret value
5. Click **Save**

**2. Clear the value in `appsettings.json`**

Add the key to `appsettings.json` with an empty string so the app config structure is documented and the key exists for local development:

```json
"MyService": {
  "ApiKey": ""
}
```

Commit this change.

**3. Add the value to local dev secrets (developer machines)**

Each developer stores their own value in `dotnet user-secrets`:

```bash
cd server/src/Batanai.Api
dotnet user-secrets set "MyService:ApiKey" "your-local-dev-value"
```

Note: user-secrets uses `:` not `__`.

**4. Update `appsettings.Development.template.json`**

Add the key with a placeholder so the template stays current for onboarding:

```json
"MyService": {
  "ApiKey": "YOUR_MYSERVICE_API_KEY_HERE"
}
```

Commit this change.

**5. Use the value in code**

Inject `IConfiguration` as normal — no Infisical-specific code needed:

```csharp
var apiKey = _configuration["MyService:ApiKey"];
```

The Infisical provider transparently populates it in production; user-secrets populates it in development.

**6. Verify on the server after next deployment**

After deploying, check logs for the new secret:

```bash
sudo journalctl -u Batanai-api -n 100 --no-pager | grep Infisical
```

You should see a line like:
```
[Infisical] Secret 'MyService:ApiKey' length: raw=32 clean=32
```

---

## Infisical Secret Names Reference

Secrets must be named exactly as below in the Infisical **prod** environment:

| Infisical Secret Name | Config Key |
|---|---|
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` |
| `Jwt__Key` | `Jwt:Key` |
| `Jwt__MfaTempKey` | `Jwt:MfaTempKey` |
| `Vapid__PrivateKey` | `Vapid:PrivateKey` |
| `Vapid__PublicKey` | `Vapid:PublicKey` |
| `Vapid__Subject` | `Vapid:Subject` |
| `WebAuthn__Origin` | `WebAuthn:Origin` |
| `WebAuthn__RelyingPartyId` | `WebAuthn:RelyingPartyId` |

