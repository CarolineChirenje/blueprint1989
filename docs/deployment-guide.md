# Divvy Deployment Guide

**Frontend:** `https://divvy.elroitec.com`  
**Backend API:** `https://divvyapi.elroitec.com`  
**Stack:** .NET 10 Â· PostgreSQL Â· Angular 21 PWA Â· Nginx Â· Let's Encrypt Â· Ubuntu 22.04

---

## Overview

| What | Domain | Served by |
|------|--------|-----------|
| Angular PWA (frontend) | `divvy.elroitec.com` | Nginx (static files) |
| .NET API (backend) | `divvyapi.elroitec.com` | Nginx → Kestrel on port 1954 |
| PostgreSQL | localhost only | PostgreSQL 15+ |

---

## Prerequisites

---

## Part 1 â€” DNS (Namesilo)

Do this **first** â€” propagation can take up to 30 minutes.

In your **Namesilo DNS Manager** for `elroitec.com`, add two A records:

| Type | Host | Value | TTL |
|------|------|-------|-----|
| A | `Divvy` | `<your-server-ip>` | 3600 |
| A | `divvyapi` | `<your-server-ip>` | 3600 |

Verify propagation before continuing:
```bash
nslookup divvy.elroitec.com
nslookup divvyapi.elroitec.com
```
Both should resolve to your server IP.

---

## Part 2 â€” Server Setup (Ubuntu)

SSH into your server:
```bash
ssh ubuntu@<your-server-ip>
```

### 2.1 Update the system
```bash
sudo apt update && sudo apt upgrade -y
```

### 2.2 Install .NET 10 Runtime
```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt update
sudo apt install -y aspnetcore-runtime-10.0
dotnet --version   # should show 10.x.x
```

### 2.3 Install PostgreSQL
```bash
sudo apt install -y postgresql postgresql-contrib
sudo systemctl enable postgresql
sudo systemctl start postgresql
```

### 2.4 Create the database and user
```bash
sudo -u postgres psql
```
Inside the `psql` prompt:
```sql
CREATE USER "Divvy" WITH PASSWORD '3lr01tec2024##';
CREATE DATABASE "Divvy" OWNER "Divvy";
GRANT ALL PRIVILEGES ON DATABASE "Divvy" TO "Divvy";
\q
```

### 2.5 Allow password authentication for the Divvy user

Open the PostgreSQL host-based authentication config:
```bash
sudo nano /etc/postgresql/*/main/pg_hba.conf
```
Find the line(s) for local/127.0.0.1 connections and ensure the method is `md5` (not `peer`):
```
local   all   all   md5
host    all   all   127.0.0.1/32   md5
```
Restart PostgreSQL:
```bash
sudo systemctl restart postgresql
```

### 2.6 Install Nginx
```bash
sudo apt install -y nginx
sudo systemctl enable nginx
sudo systemctl start nginx
```

### 2.7 Install Certbot
```bash
sudo apt install -y certbot python3-certbot-nginx
```

### 2.8 Create deployment directories
```bash
sudo mkdir -p /home/elroitecProjects/app
sudo mkdir -p /home/elroitecProjects/api
sudo chown -R ubuntu:ubuntu /home/elroitecProjects
```

### 2.9 Configure the firewall
```bash
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'
sudo ufw enable
sudo ufw status
```

---

## Part 3 â€” Connect to PostgreSQL via DBeaver

DBeaver connects through an **SSH tunnel** â€” PostgreSQL is never exposed on a public port.

1. Open DBeaver â†’ **New Database Connection** â†’ choose **PostgreSQL**
2. In the **Main** tab:
   - **Host:** `localhost`
   - **Port:** `5432`
   - **Database:** `Divvy`
   - **Username:** `Divvy`
   - **Password:** `3lr01tec2024##`
3. Click the **SSH** tab â†’ enable **Use SSH tunnel**:
   - **Host/IP:** `<your-server-ip>`
   - **Port:** `22`
   - **Username:** `ubuntu`
   - **Authentication:** Public Key (browse to your `.pem` file) or Password
4. Click **Test Connection** â†’ should say "Connected"
5. Click **Finish**

You can now browse tables, run SQL queries, and inspect data directly.

---

## Part 4 â€” Frontend â€” Settings to Update Before Building

### 4.1 `client/src/environments/environment.prod.ts`

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://divvyapi.elroitec.com/api',           // â† backend API domain
  excelExportUrl: 'https://divvyapi.elroitec.com/api/export/excel',
  googleDriveFolderId: 'your-google-drive-folder-id',
  vapidPublicKey: 'BFEfRC079NVJZjR3LC5-a7Jajc3tFvJtjaVdq9ClWX-uLuP58nTRhnYXYuivaGiFIeq9Z2lcrKZ9hx1uOhoIyVU',  // â† must match Vapid:PublicKey on server
  version: '1.0.0',
};
```

> **Critical:** `vapidPublicKey` must exactly match `Vapid:PublicKey` in `appsettings.Production.json` on the server, otherwise push notifications will fail.

### 4.2 Build the Angular app
```powershell
cd C:\dev\6299\client
npx ng build --configuration production
```
Output: `client\dist\Divvy\browser\`

### 4.3 Copy the frontend to the server
```powershell
scp -r C:\dev\6299\client\dist\Divvy\browser\* ubuntu@<your-server-ip>:/home/elroitecProjects/app/
```
Or use **WinSCP** / **FileZilla** (SFTP) â€” drag the contents of `browser\` into `/home/elroitecProjects/app/`.

---

## Part 5 â€” Backend â€” Settings to Update Before Deploying

### 5.1 `server/src/Divvy.Api/appsettings.Production.json`

This is the **only** file that needs updating for a production deploy:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=Divvy;Username=Divvy;Password=3lr01tec2024##"
  },
  "AppSettings": {
    "AppName": "Divvy",
    "PasswordExpirationDays": 30,
    "TimeZone": "AUS Eastern Standard Time",
    "AdminSignupPin": "42115"
  },
  "Jwt": {
    "Key": "Production2026SecureJwtKeyForDivvyAuthenticationChangeInProductionMin32Chars3K9P",
    "MfaTempKey": "ProductionMfaTempKey2026SecureForTwoFactorAuthChangeInProductionMinimum32CharsLongRequired6W",
    "Issuer": "divvyapi.elroitec.com",
    "Audience": "divvyapi.elroitec.com",
    "ExpirationMinutes": 30
  },
  "Vapid": {
    "Subject": "mailto:elroitec@gmail.com",
    "PublicKey": "BFEfRC079NVJZjR3LC5-a7Jajc3tFvJtjaVdq9ClWX-uLuP58nTRhnYXYuivaGiFIeq9Z2lcrKZ9hx1uOhoIyVU",
    "PrivateKey": "YOUR_VAPID_PRIVATE_KEY"
  },
  "WebAuthn": {
    "RelyingPartyId": "divvy.elroitec.com",
    "RelyingPartyName": "Divvy",
    "Origin": "https://divvy.elroitec.com"
  },
  "AllowedHosts": "divvyapi.elroitec.com;divvy.elroitec.com;localhost;127.0.0.1",
  "Cors": {
    "AllowedOrigins": [
      "https://divvy.elroitec.com",
      "https://divvyapi.elroitec.com"
    ]
  },
  "GoogleDrive": {
    "ClientId": "PRODUCTION_CLIENT_ID",
    "ClientSecret": "PRODUCTION_CLIENT_SECRET",
    "RedirectUri": "https://divvy.elroitec.com/api/auth/google-callback"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Error",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**Settings explained:**

| Setting | Why it matters |
|---------|----------------|
| `ConnectionStrings:DefaultConnection` | Must match the PostgreSQL user/password created in Part 2.4 |
| `Jwt:Issuer` + `Jwt:Audience` | **Must both be set** â€” the API validates these on every request. Set to `divvyapi.elroitec.com` |
| `Jwt:Key` | Must be at least 32 characters â€” keep it secret |
| `Vapid:PublicKey` | Must match `vapidPublicKey` in `environment.prod.ts` |
| `Cors:AllowedOrigins` | Must include `https://divvy.elroitec.com` (the frontend origin) |
| `AllowedHosts` | Must include both subdomains, semicolon-separated |
| `WebAuthn:RelyingPartyId` | Must be the **frontend** domain, not the API domain |

### 5.2 Publish the .NET API
```powershell
cd C:\dev\6299\server\src\Divvy.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o C:\dev\6299\publish\api
```

### 5.3 Copy the API to the server
```powershell
scp -r C:\dev\6299\publish\api\* ubuntu@<your-server-ip>:/home/elroitecProjects/api/
```
Or use **WinSCP** / **FileZilla** and drag `publish\api\` contents into `/home/elroitecProjects/api/`.

---

## Part 6 â€” Run Database Migrations

**Option A â€” From Windows (recommended for first deploy):**
```powershell
cd C:\dev\6299\server\src\Divvy.Api
$env:ConnectionStrings__DefaultConnection = "Host=<your-server-ip>;Port=5432;Database=Divvy;Username=Divvy;Password=3lr01tec2024##"
dotnet ef database update
```

**Option B â€” From the server after deploying:**
```bash
cd /home/elroitecProjects/api
export ASPNETCORE_ENVIRONMENT=Production
dotnet Divvy.Api.dll --migrate
```

---

## Part 7 â€” Create the systemd Service

On the server:
```bash
sudo nano /etc/systemd/system/divvy-api.service
```
Paste:
```ini
[Unit]
Description=Divvy .NET API
After=network.target postgresql.service

[Service]
WorkingDirectory=/home/elroitecProjects/api
ExecStart=/usr/bin/dotnet /home/elroitecProjects/api/Divvy.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=divvy-api
User=ubuntu
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:1954
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable divvy-api
sudo systemctl start divvy-api
sudo systemctl status divvy-api
```

Confirm the API is responding:
```bash
curl -s http://localhost:1954/api/health
```

---

## Part 8 â€” Configure Nginx

### 8.1 Frontend site (`divvy.elroitec.com`)
```bash
sudo nano /etc/nginx/sites-available/Divvy
```
Paste:
```nginx
server {
    listen 80;
    server_name divvy.elroitec.com;

    root /home/elroitecProjects/app;
    index index.html;

    # Service worker â€” no cache
    location = /ngsw-worker.js {
        add_header Cache-Control "no-store, no-cache, must-revalidate, proxy-revalidate, max-age=0";
        add_header Service-Worker-Allowed "/";
        try_files $uri =404;
    }

    location = /custom-sw.js {
        add_header Cache-Control "no-store, no-cache, must-revalidate, proxy-revalidate, max-age=0";
        add_header Service-Worker-Allowed "/";
        try_files $uri =404;
    }

    # PWA manifest
    location = /manifest.webmanifest {
        add_header Content-Type "application/manifest+json";
        try_files $uri =404;
    }

    # Static assets â€” long cache
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot|webp)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
        try_files $uri =404;
    }

    # Angular router â€” fallback to index.html
    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

### 8.2 Backend site (`divvyapi.elroitec.com`)
```bash
sudo nano /etc/nginx/sites-available/Divvypi
```
Paste:
```nginx
server {
    listen 80;
    server_name divvyapi.elroitec.com;

    location / {
        proxy_pass         http://127.0.0.1:1954;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        proxy_connect_timeout 10s;
        proxy_read_timeout    120s;
        proxy_send_timeout    120s;
    }
}
```

### 8.3 Enable both sites
```bash
sudo ln -s /etc/nginx/sites-available/Divvy   /etc/nginx/sites-enabled/
sudo ln -s /etc/nginx/sites-available/divvyapi /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Part 9 â€” SSL Certificates (Let's Encrypt)

Get certificates for both domains in one command:
```bash
sudo certbot --nginx -d divvy.elroitec.com -d divvyapi.elroitec.com
```

Follow the prompts:
- Enter your email address
- Agree to terms (`A`)
- Choose **Redirect** (option 2) to force HTTPS on both domains

Certbot automatically updates both Nginx configs to add port 443 and HTTPâ†’HTTPS redirects.

Verify auto-renewal:
```bash
sudo certbot renew --dry-run
```

---

## Part 10 â€” Verify Everything

```bash
# Services running
sudo systemctl status divvy-api
sudo systemctl status nginx

# API on localhost
curl -s http://localhost:1954/api/health

# Frontend HTTPS (should return 200)
curl -s -o /dev/null -w "%{http_code}" https://divvy.elroitec.com/

# API HTTPS (should return 200 or 404, not 502/504)
curl -s -o /dev/null -w "%{http_code}" https://divvyapi.elroitec.com/api/health

# Live API logs
sudo journalctl -u divvy-api -f
```

Open a browser:
- `https://divvy.elroitec.com` â†’ Angular login screen 
- `https://divvyapi.elroitec.com/api/swagger` â†’ Swagger UI 

---

## Redeployment Workflow (future updates)

### Frontend only
```powershell
cd C:\dev\6299\client
npx ng build --configuration production
scp -r .\dist\Divvy\browser\* ubuntu@<your-server-ip>:/home/elroitecProjects/app/
```
No restart needed â€” Nginx serves files directly.

### Backend only
```powershell
cd C:\dev\6299\server\src\Divvy.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o C:\dev\6299\publish\api
scp -r C:\dev\6299\publish\api\* ubuntu@<your-server-ip>:/home/elroitecProjects/api/
```
Then on the server:
```bash
sudo systemctl restart divvy-api
sudo systemctl status divvy-api
```

### Config only (no rebuild)
```bash
nano /home/elroitecProjects/api/appsettings.Production.json
sudo systemctl restart divvy-api
```

### Generate new VAPID keys
```powershell
cd C:\dev\6299\server\vapid-keygen
dotnet run
```
Copy the output into `appsettings.Production.json` (`Vapid` section) **and** `environment.prod.ts` (`vapidPublicKey`), then rebuild and redeploy both.

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| `504 Gateway Timeout` | API not running | `sudo systemctl restart divvy-api` ; check `sudo journalctl -u divvy-api -n 50` |
| `400 Bad Request - Invalid Hostname` | Domain missing from `AllowedHosts` | Add domain to `AllowedHosts` in `appsettings.Production.json`, restart API |
| `401 Unauthorized` on all API calls | `Jwt:Issuer`/`Jwt:Audience` not set in token | Ensure both are set in `appsettings.Production.json` and match â€” `divvyapi.elroitec.com` |
| `CORS error` in browser | Frontend origin not in allowed list | Add `https://divvy.elroitec.com` to `Cors:AllowedOrigins`, restart API |
| `styles.css 404` | Stale service worker cache | DevTools â†’ Application â†’ Service Workers â†’ Unregister â†’ hard refresh |
| `404` on page refresh | Nginx missing Angular fallback | Ensure `try_files $uri $uri/ /index.html;` in `Divvy` Nginx config |
| DB connection error | Wrong credentials or DB not created | Check `ConnectionStrings` in `appsettings.Production.json`; verify DB via DBeaver |
| SSL certificate error | Cert not issued or expired | `sudo certbot certificates` ; `sudo certbot renew` |
| API not starting | Missing config or .NET not installed | `sudo journalctl -u divvy-api -n 100 --no-pager` ; `dotnet --version` |

---

## Part 1 â€” Prepare the Linux Server

SSH into your server:

```bash
ssh deploy@<server-ip>
```

### 1.1 Update the system

```bash
sudo apt update && sudo apt upgrade -y
```

### 1.2 Install .NET 10 Runtime

```bash
# Add the Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt update
sudo apt install -y aspnetcore-runtime-10.0
```

Verify:

```bash
dotnet --version   # should show 10.x.x
```

### 1.3 Install PostgreSQL

```bash
sudo apt install -y postgresql postgresql-contrib
sudo systemctl enable postgresql
sudo systemctl start postgresql
```

### 1.4 Create the database and user

```bash
sudo -u postgres psql
```

Inside the psql prompt:

```sql
CREATE USER Divvy WITH PASSWORD '3lr01tec2024##';
CREATE DATABASE "Divvy" OWNER Divvy;
GRANT ALL PRIVILEGES ON DATABASE "Divvy" TO Divvy;
\q
```

### 1.5 Install Nginx

```bash
sudo apt install -y nginx
sudo systemctl enable nginx
sudo systemctl start nginx
```

### 1.6 Install Certbot (Let's Encrypt)

```bash
sudo apt install -y certbot python3-certbot-nginx
```

### 1.7 Create deployment directories

```bash
sudo mkdir -p /home/elroitecProjects/app     # Angular frontend
sudo mkdir -p /home/elroitecProjects/api     # .NET API
sudo chown -R deploy:deploy /home/elroitecProjects
```

---

## Part 2 â€” Build & Publish on Windows (your laptop)

### 2.1 Update environment.prod.ts

In `client/src/environments/environment.prod.ts`:

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://divvy.elroitec.com/api',
  excelExportUrl: 'https://divvy.elroitec.com/api/export',
  googleDriveFolderId: 'your-google-drive-folder-id',
  vapidPublicKey: 'YOUR_ACTUAL_VAPID_PUBLIC_KEY',  // from server appsettings.json Vapid:PublicKey
};
```

### 2.2 Build the Angular app

```powershell
cd C:\dev\6299\client
npx ng build --configuration production
```

Output will be in: `client\dist\Divvy\browser\`

### 2.3 Publish the .NET API

```powershell
cd C:\dev\6299\server\src\Divvy.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o C:\dev\6299\publish\api
```

---

## Part 3 â€” Copy Files to the Server

You can copy files to the server using either the command line (SCP) or a graphical tool like WinSCP or FileZilla.
If you prefer a UI:

1. Download and install WinSCP or FileZilla on your Windows machine.
2. Connect to your server using SFTP (host: <server-ip>, username: your server user, password or SSH key).
3. Navigate to `/home/elroitecProjects` on the server.
4. Drag and drop your Angular app and API folders/files from your local machine to the server.

Replace `deploy@<server-ip>` with your actual user and IP if using SCP.

### 3.1 Copy the Angular app

```powershell
scp -r C:\dev\6299\client\dist\Divvy\browser\* deploy@<server-ip>:/home/elroitecProjects/app/
```

### 3.2 Copy the API

```powershell
scp -r C:\dev\6299\publish\api\* deploy@<server-ip>:/home/elroitecProjects/api/
```

---

## Part 4 â€” Configure the API on the Server

SSH back into the server:

```bash
ssh deploy@<server-ip>
```

### 4.1 Create Production appsettings

The published folder already contains `appsettings.Production.json`. Edit it with the real values:

```bash
nano /var/www/Divvy/api/appsettings.Production.json
```

Replace the contents with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=Divvy;Username=Divvy;Password=YourStrongPasswordHere"
  },
  "AppSettings": {
    "AppName": "Divvy",
    "PasswordExpirationDays": 30,
    "TimeZone": "AUS Eastern Standard Time",
    "AdminSignupPin": "CHANGE-THIS-TO-A-SECRET-PIN"
  },
  "Jwt": {
    "Key": "CHANGE-THIS-TO-A-LONG-RANDOM-SECRET-AT-LEAST-32-CHARS",
    "MfaTempKey": "CHANGE-THIS-TO-ANOTHER-LONG-RANDOM-SECRET-AT-LEAST-32-CHARS",
    "Issuer": "divvyapi.elroitec.com",
    "Audience": "divvyapi.elroitec.com",
    "ExpirationMinutes": 30
  },
  "Vapid": {
    "Subject": "mailto:admin@elroitec.com",
    "PublicKey": "YOUR_VAPID_PUBLIC_KEY",
    "PrivateKey": "YOUR_VAPID_PRIVATE_KEY"
  },
  "GoogleDrive": {
    "ClientId": "YOUR_PRODUCTION_CLIENT_ID",
    "ClientSecret": "YOUR_PRODUCTION_CLIENT_SECRET",
    "RedirectUri": "https://divvy.elroitec.com/api/auth/google-callback"
  },
  "AllowedHosts": "divvy.elroitec.com",
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Error",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Urls": "http://localhost:1954"
}
```

Save and exit: `Ctrl+O`, `Enter`, `Ctrl+X`

### 4.2 Prepare database user and run migrations

After the API has started for the first time (code-first will create the database automatically), you need to manually create the PostgreSQL user and grant privileges:

```bash
sudo -u postgres psql

# Inside psql prompt:
CREATE USER Divvy WITH PASSWORD '3lr01tec2024##';
GRANT ALL PRIVILEGES ON DATABASE "Divvy" TO Divvy;
\q
```

Then run migrations:

```bash
cd /home/elroitecProjects/api

# Set the environment so the API uses Production appsettings
export ASPNETCORE_ENVIRONMENT=Production

dotnet Divvy.Api.dll -- --migrate
```

> **Note:** If the above `--migrate` flag is not wired up, run migrations from your Windows machine using the EF CLI against the production database instead:
>
> ```powershell
> # On Windows, pointing at the production DB
> cd C:\dev\6299\server\src\Divvy.Api
> $env:ConnectionStrings__DefaultConnection = "Host=<server-ip>;Port=5432;Database=Divvy;Username=Divvy;Password=YourStrongPasswordHere"
> dotnet ef database update
> ```

---

## Part 5 â€” Create the systemd Service

This keeps the API running and restarts it on failure.

```bash
sudo nano /etc/systemd/system/divvy-api.service
```

Paste:

```ini
[Unit]
Description=Divvy .NET API
After=network.target postgresql.service

[Service]
WorkingDirectory=/home/elroitecProjects/api
ExecStart=/usr/bin/dotnet /home/elroitecProjects/api/Divvy.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=divvy-api
User=deploy
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Save and exit, then enable and start the service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable divvy-api
sudo systemctl start divvy-api
```

Check it is running:

```bash
sudo systemctl status divvy-api
```

You should see `Active: active (running)`. Also confirm it is listening:

```bash
curl http://localhost:1954/api/health   # or any valid endpoint
```

---

## Part 6 â€” Configure Nginx

### 6.1 Create the site config

```bash
sudo nano /etc/nginx/sites-available/Divvy
```

Paste:

```nginx
server {
    listen 80;
    server_name divvy.elroitec.com;

    # ------------------------------------------------------------
    # Angular PWA â€” served from /home/elroitecProjects/app
    # ------------------------------------------------------------
    root /home/elroitecProjects/app;
    index index.html;

    # Service worker must be served without cache
    location = /ngsw-worker.js {
      add_header Cache-Control "no-store, no-cache, must-revalidate, proxy-revalidate, max-age=0";
      add_header Service-Worker-Allowed "/";
      try_files $uri =404;
    }

    location = /custom-sw.js {
      add_header Cache-Control "no-store, no-cache, must-revalidate, proxy-revalidate, max-age=0";
      add_header Service-Worker-Allowed "/";
      try_files $uri =404;
    }

    # PWA manifest
    location = /manifest.webmanifest {
      add_header Content-Type "application/manifest+json";
      try_files $uri =404;
    }

    # API – proxied to Kestrel on port 1954
    location /api/ {
      proxy_pass         http://localhost:1954/api/;
      proxy_http_version 1.1;
      proxy_set_header   Upgrade $http_upgrade;
      proxy_set_header   Connection keep-alive;
      proxy_set_header   Host $host;
      proxy_set_header   X-Real-IP $remote_addr;
      proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
      proxy_set_header   X-Forwarded-Proto $scheme;
      proxy_cache_bypass $http_upgrade;
      proxy_read_timeout 120s;
    }

    # Angular router â€” fallback all non-file requests to index.html
    location / {
      try_files $uri $uri/ /index.html;
    }

    # Static assets â€” long cache
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot|webp)$ {
      expires 1y;
      add_header Cache-Control "public, immutable";
      try_files $uri =404;
    }
}
```

### 6.2 Enable the site

```bash
sudo ln -s /etc/nginx/sites-available/Divvy /etc/nginx/sites-enabled/
sudo nginx -t          # must say "syntax is ok"
sudo systemctl reload nginx
```

---

## Part 7 â€” SSL with Let's Encrypt

```bash
sudo certbot --nginx -d divvy.elroitec.com
```

Follow the prompts:
- Enter your email address
- Agree to terms of service (`A`)
- Choose `2` (Redirect HTTP â†’ HTTPS)

Certbot will automatically:
- Obtain the certificate
- Update your nginx config to listen on port 443
- Set up HTTP â†’ HTTPS redirect

Verify auto-renewal works:

```bash
sudo certbot renew --dry-run
```

---

## Part 8 â€” Configure Firewall

```bash
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'   # ports 80 and 443
sudo ufw enable
sudo ufw status
```

---

## Part 9 â€” Verify Everything is Running

```bash
# API service status
sudo systemctl status divvy-api

# Nginx status
sudo systemctl status nginx

# API responding on localhost
curl -s -o /dev/null -w "%{http_code}" http://localhost:1954/api/health

# Tail the API logs
sudo journalctl -u divvy-api -f
```

Open a browser and visit:
- **https://divvy.elroitec.com** â€” Angular app should load
- **https://divvy.elroitec.com/api/swagger** â€” Swagger UI (if enabled in production)

---

## Redeployment Workflow (future updates)

### Update the Angular app only

```powershell
# On Windows
cd C:\dev\6299\client
npx ng build --configuration production
scp -r .\dist\Divvy\browser\* deploy@<server-ip>:/home/elroitecProjects/app/
```

No service restart needed â€” Nginx serves files directly.

### Update the API only

```powershell
# On Windows
cd C:\dev\6299\server\src\Divvy.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o C:\dev\6299\publish\api
scp -r C:\dev\6299\publish\api\* deploy@<server-ip>:/var/www/Divvy/api/
```

Then restart the service:

```bash
# On server
sudo systemctl restart divvy-api
sudo systemctl status divvy-api
```

### Update config only (e.g. CORS origins, VAPID keys)

```bash
# On server â€” edit the file, then restart
nano /var/www/Divvy/api/appsettings.Production.json
sudo systemctl restart divvy-api
```

### Apply only migrations that have not been applied 

dotnet ef migrations script --idempotent -o 20260321.sql

Run this command in the divvyapi folder

---

### Troubleshoot deployed API
sudo journalctl -u divvy-api.service -n 200 --no-pager
dotnet /home/elroitecProjects/api/Divvy.Api.dll
dotnet --info
sudo cat /etc/systemd/system/divvy-api.service

## Troubleshooting

| Problem | Command to investigate |
|---------|----------------------|
| API not starting | `sudo journalctl -u divvy-api -n 50 --no-pager` |
| Nginx 502 Bad Gateway | Check API is running: `sudo systemctl status divvy-api` |
| CORS errors in browser | Confirm `AllowedHosts` in `appsettings.Production.json` matches the domain |
| SSL not working | `sudo certbot certificates` â€” check expiry and domain |
| DB connection errors | `sudo -u postgres psql -c "\l"` â€” verify Divvy DB exists |
| Service worker not updating | Hard reload (Ctrl+Shift+R) or clear site data in DevTools > Application |
| 404 on page refresh | Confirm `try_files $uri $uri/ /index.html;` is in nginx config |


