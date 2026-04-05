# Batanai Deployment Guide

**Frontend:** `https://batanai.elroitec.com` (port 4300)  
**Backend API:** `https://batanaiapi.elroitec.com` (port 1954)  
**Stack:** .NET 10 � PostgreSQL � Angular 21 PWA � Nginx � Let's Encrypt � Ubuntu 22.04

---

## Overview

| What | Port | Domain | Served by |
|------|------|--------|-----------|
| Angular PWA (frontend) | 4300 | `batanai.elroitec.com` | Nginx ? static files |
| .NET API (backend) | 1954 | `batanaiapi.elroitec.com` | Nginx ? Kestrel |
| PostgreSQL | 5432 | localhost only | PostgreSQL 15+ |

---

## Step 1 � DNS (Namesilo)

Do this **first** � propagation can take up to 30 minutes.

In your **Namesilo DNS Manager** for `elroitec.com`, add two A records:

| Type | Host | Value | TTL |
|------|------|-------|-----|
| A | `Batanai` | `<server-ip>` | 3600 |
| A | `Batanaiapi` | `<server-ip>` | 3600 |

Verify:
```bash
nslookup batanai.elroitec.com
nslookup batanaiapi.elroitec.com
```

---

## Step 2 � Server Setup (Ubuntu)

```bash
ssh ubuntu@<server-ip>
```

### 2.1 Update system
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
dotnet --version
```

### 2.3 Install PostgreSQL
```bash
sudo apt install -y postgresql postgresql-contrib
sudo systemctl enable postgresql
sudo systemctl start postgresql
```

### 2.4 Create database and user
```bash
sudo -u postgres psql
```
```sql
CREATE USER "Batanai" WITH PASSWORD '3lr01tec2024##';
CREATE DATABASE "Batanai" OWNER "Batanai";
GRANT ALL PRIVILEGES ON DATABASE "Batanai" TO "Batanai";
\q
```

### 2.5 Allow password authentication
```bash
sudo nano /etc/postgresql/*/main/pg_hba.conf
```
Ensure these lines:
```
local   all   all   md5
host    all   all   127.0.0.1/32   md5
```
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
sudo mkdir -p /home/elroitecProjects/Batanai/app
sudo mkdir -p /home/elroitecProjects/Batanai/api
sudo chown -R ubuntu:ubuntu /home/elroitecProjects/Batanai
```

### 2.9 Configure firewall (open ports 4300 and 1954)
```bash
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'
sudo ufw allow 4300/tcp
sudo ufw allow 1954/tcp
sudo ufw enable
sudo ufw status
```

Verify ports are open:
```bash
sudo ufw status numbered
```
Expected output includes:
```
4300/tcp    ALLOW IN    Anywhere
1954/tcp    ALLOW IN    Anywhere
80/tcp      ALLOW IN    Anywhere
443/tcp     ALLOW IN    Anywhere
```

---

## Step 3 � Connect to PostgreSQL via DBeaver

DBeaver connects through an **SSH tunnel** � PostgreSQL is never exposed publicly.

1. Open DBeaver ? **New Database Connection** ? **PostgreSQL**
2. **Main** tab:
   - Host: `localhost` � Port: `5432` � Database: `Batanai` � Username: `Batanai` � Password: `3lr01tec2024##`
3. **SSH** tab ? enable **Use SSH tunnel**:
   - Host: `<server-ip>` � Port: `22` � Username: `ubuntu` � Auth: Public Key (`.pem` file)
4. **Test Connection** ? **Finish**

---

## Step 4 � Build Frontend (on Windows)

### 4.1 Update `client/src/environments/environment.prod.ts`

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://batanaiapi.elroitec.com/api',
  vapidPublicKey: 'BFEfRC079NVJZjR3LC5-a7Jajc3tFvJtjaVdq9ClWX-uLuP58nTRhnYXYuivaGiFIeq9Z2lcrKZ9hx1uOhoIyVU',
  version: '1.0.0',
  docsUrl: 'https://batanai-docs.pages.dev',
};
```

> **Critical:** `vapidPublicKey` must match `Vapid:PublicKey` in `appsettings.Production.json`.

### 4.2 Build
```powershell
cd C:\dev\Batanai\client
npx ng build --configuration production
```
Output: `C:\Publish\Batanai\app\` (configured in `angular.json` ? `outputPath`)

### 4.3 Copy to server
```powershell
scp -r C:\Publish\Batanai\app\* ubuntu@<server-ip>:/home/elroitecProjects/app/
```

---

## Step 5 � Build & Deploy Backend (on Windows)

### 5.1 Update `server/src/Batanai.Api/appsettings.Production.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=Batanai;Username=Batanai;Password=3lr01tec2024##"
  },
  "AppSettings": {
    "AppName": "Batanai",
    "PasswordExpirationDays": 30,
    "TimeZone": "AUS Eastern Standard Time",
    "AdminSignupPin": "42115"
  },
  "Jwt": {
    "Key": "Production2026SecureJwtKeyForBatanaiAuthenticationChangeInProductionMin32Chars3K9P",
    "MfaTempKey": "ProductionMfaTempKey2026SecureForTwoFactorAuthChangeInProductionMinimum32CharsLongRequired6W",
    "Issuer": "batanaiapi.elroitec.com",
    "Audience": "batanaiapi.elroitec.com",
    "ExpirationMinutes": 30
  },
  "Vapid": {
    "Subject": "mailto:elroitec@gmail.com",
    "PublicKey": "BFEfRC079NVJZjR3LC5-a7Jajc3tFvJtjaVdq9ClWX-uLuP58nTRhnYXYuivaGiFIeq9Z2lcrKZ9hx1uOhoIyVU",
    "PrivateKey": "YOUR_VAPID_PRIVATE_KEY"
  },
  "WebAuthn": {
    "RelyingPartyId": "batanai.elroitec.com",
    "RelyingPartyName": "Batanai",
    "Origin": "https://batanai.elroitec.com"
  },
  "AllowedHosts": "batanaiapi.elroitec.com;batanai.elroitec.com;localhost;127.0.0.1",
  "Cors": {
    "AllowedOrigins": [
      "https://batanai.elroitec.com",
      "https://batanaiapi.elroitec.com"
    ]
  },
  "GoogleDrive": {
    "ClientId": "PRODUCTION_CLIENT_ID",
    "ClientSecret": "PRODUCTION_CLIENT_SECRET",
    "RedirectUri": "https://batanai.elroitec.com/api/auth/google-callback"
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

**Key settings:**

| Setting | Must be |
|---------|---------|
| `Jwt:Issuer` + `Jwt:Audience` | `batanaiapi.elroitec.com` |
| `Jwt:Key` | At least 32 chars, keep secret |
| `Vapid:PublicKey` | Must match `environment.prod.ts` |
| `Cors:AllowedOrigins` | Must include `https://batanai.elroitec.com` |
| `AllowedHosts` | Both subdomains, semicolon-separated |
| `WebAuthn:RelyingPartyId` | Frontend domain (`batanai.elroitec.com`) |

### 5.2 Publish
```powershell
cd C:\dev\Batanai\server\src\Batanai.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o C:\dev\Batanai\publish\api
```

### 5.3 Copy to server
```powershell
scp -r C:\dev\Batanai\publish\api\* ubuntu@<server-ip>:/home/elroitecProjects/Batanai/api/
```

---

## Step 6 � Run Database Migrations

**Option A � From Windows (recommended for first deploy):**
```powershell
cd C:\dev\Batanai\server\src\Batanai.Api
$env:ConnectionStrings__DefaultConnection = "Host=<server-ip>;Port=5432;Database=Batanai;Username=Batanai;Password=3lr01tec2024##"
dotnet ef database update
```

**Option B � From the server:**
```bash
cd /home/elroitecProjects/Batanai/api
export ASPNETCORE_ENVIRONMENT=Production
dotnet Batanai.Api.dll --migrate
```

**Generate idempotent SQL script (apply only unapplied migrations):**
```powershell
cd C:\dev\Batanai\server\src\Batanai.Api
dotnet ef migrations script --idempotent -o migration.sql
```

---

## Step 7 � Create systemd Service

On the server:
```bash
sudo nano /etc/systemd/system/Batanai-api.service
```

Paste:
```ini
[Unit]
Description=Batanai .NET API
After=network.target postgresql.service

[Service]
WorkingDirectory=/home/elroitecProjects/Batanai/api
ExecStart=/usr/bin/dotnet /home/elroitecProjects/Batanai/api/Batanai.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=Batanai-api
User=ubuntu
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:1954
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=Infisical__ClientSecret=YOUR_SECRET_HERE

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable Batanai-api
sudo systemctl start Batanai-api
sudo systemctl status Batanai-api
```

Verify:
```bash
curl -s http://localhost:1954/api/health
```

---

## Step 8 � Configure Nginx

### 8.1 Frontend (`batanai.elroitec.com` on port 4300)
```bash
sudo nano /etc/nginx/sites-available/Batanai
```
```nginx
server {
    listen 4300;
    server_name batanai.elroitec.com;

    root /home/elroitecProjects/app;
    index index.html;

    # Service worker � no cache
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

    # Static assets � long cache
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot|webp)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
        try_files $uri =404;
    }

    # Angular router � fallback to index.html
    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

### 8.2 Combined frontend + API proxy (alternative)

If you want a single-domain setup where the API is proxied under `/api/` on port 80 (no separate `batanaiapi` subdomain needed):

```bash
sudo nano /etc/nginx/sites-available/Batanai
```
```nginx
server {
    server_name batanai.elroitec.com;

    # Frontend app location
    root /home/elroitecProjects/Batanai/app;
    index index.html;

    # API proxy to .NET backend
    location /api/ {
      proxy_pass         http://127.0.0.1:1954/api/;
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

    # Angular routing fallback
    location / {
      try_files $uri $uri/ /index.html;
    }

    # Static assets cache
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot|webp)$ {
      expires 1y;
      add_header Cache-Control "public, immutable";
      try_files $uri =404;
    }

    listen 80;
}
```

> **Note:** With this approach, update `environment.prod.ts` to use `apiUrl: 'https://batanai.elroitec.com/api'` instead of the separate `batanaiapi` subdomain.

### 8.3 Backend (`batanaiapi.elroitec.com` on port 1954)
```bash
sudo nano /etc/nginx/sites-available/Batanaiapi
```
```nginx
server {
    listen 80;
    server_name batanaiapi.elroitec.com;

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

### 8.4 Enable both sites
```bash
sudo ln -s /etc/nginx/sites-available/Batanaiapi /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Step 9 � SSL Certificates (Let's Encrypt)

```bash
sudo certbot --nginx -d batanai.elroitec.com -d batanaiapi.elroitec.com
```

- Enter email, agree to terms (`A`), choose **Redirect** (option 2)
- Certbot auto-updates Nginx configs for HTTPS

Verify auto-renewal:
```bash
sudo certbot renew --dry-run
```

---

## Step 10 � Verify Everything

```bash
# Services
sudo systemctl status Batanai-api
sudo systemctl status nginx

# API on localhost
curl -s http://localhost:1954/api/health

# Frontend
curl -s -o /dev/null -w "%{http_code}" https://batanai.elroitec.com:4300/

# API HTTPS
curl -s -o /dev/null -w "%{http_code}" https://batanaiapi.elroitec.com/api/health

# Firewall
sudo ufw status

# Live API logs
sudo journalctl -u Batanai-api -f
```

Open in browser:
- `https://batanai.elroitec.com:4300` ? Angular login screen
- `https://batanaiapi.elroitec.com/api/swagger` ? Swagger UI

---

## Redeployment (future updates)

### Frontend only
```powershell
cd C:\dev\Batanai\client
npx ng build --configuration production
scp -r C:\Publish\Batanai\app\* ubuntu@<server-ip>:/home/elroitecProjects/app/
```
No restart needed � Nginx serves static files directly.

### Backend only
```powershell
cd C:\dev\Batanai\server\src\Batanai.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o C:\dev\Batanai\publish\api
scp -r C:\dev\Batanai\publish\api\* ubuntu@<server-ip>:/home/elroitecProjects/Batanai/api/
```
Then on server:
```bash
sudo systemctl restart Batanai-api
sudo systemctl status Batanai-api
```

### Config only (no rebuild)
```bash
nano /home/elroitecProjects/Batanai/api/appsettings.Production.json
sudo systemctl restart Batanai-api
```

### Generate new VAPID keys
```powershell
cd C:\dev\Batanai\server\vapid-keygen
dotnet run
```
Copy output into `appsettings.Production.json` (`Vapid` section) **and** `environment.prod.ts` (`vapidPublicKey`), then rebuild and redeploy both.

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| `504 Gateway Timeout` | API not running | `sudo systemctl restart Batanai-api` then `sudo journalctl -u Batanai-api -n 50` |
| `400 Bad Request - Invalid Hostname` | Domain missing from `AllowedHosts` | Add domain to `AllowedHosts` in `appsettings.Production.json`, restart API |
| `401 Unauthorized` on all calls | `Jwt:Issuer`/`Jwt:Audience` mismatch | Ensure both are `batanaiapi.elroitec.com` in `appsettings.Production.json` |
| `CORS error` in browser | Frontend origin not allowed | Add `https://batanai.elroitec.com` to `Cors:AllowedOrigins`, restart API |
| `styles.css 404` | Stale service worker | DevTools ? Application ? Service Workers ? Unregister ? hard refresh |
| `404` on page refresh | Missing Angular fallback | Ensure `try_files $uri $uri/ /index.html;` in Nginx config |
| DB connection error | Wrong credentials | Check `ConnectionStrings` in `appsettings.Production.json` |
| SSL certificate error | Cert expired | `sudo certbot certificates` then `sudo certbot renew` |
| API not starting | Missing runtime | `sudo journalctl -u Batanai-api -n 100 --no-pager` and `dotnet --version` |
| Port not reachable | Firewall blocking | `sudo ufw allow 4300/tcp` and `sudo ufw allow 1954/tcp` |

### Useful debug commands
```bash
sudo journalctl -u Batanai-api.service -n 200 --no-pager
dotnet /home/elroitecProjects/Batanai/api/Batanai.Api.dll
dotnet --info
sudo cat /etc/systemd/system/Batanai-api.service
sudo ufw status numbered
sudo ss -tlnp | grep -E '4300|1954'
```


