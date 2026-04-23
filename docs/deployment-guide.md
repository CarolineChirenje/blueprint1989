# Blueprint1989 Deployment Guide

**Frontend:** `https://blueprint1989.elroitec.com` (port 4300)  
**Backend API:** `https://blueprint1989api.elroitec.com` (port 1954)  
**Stack:** .NET 10 � PostgreSQL � Angular 21 PWA � Nginx � Let's Encrypt � Ubuntu 22.04

---

## Overview

| What | Port | Domain | Served by |
|------|------|--------|-----------|
| Angular PWA (frontend) | 4300 | `blueprint1989.elroitec.com` | Nginx ? static files |
| .NET API (backend) | 1954 | `blueprint1989api.elroitec.com` | Nginx ? Kestrel |
| PostgreSQL | 5432 | localhost only | PostgreSQL 15+ |

---

## Step 1 � DNS (Namesilo)

Do this **first** � propagation can take up to 30 minutes.

In your **Namesilo DNS Manager** for `elroitec.com`, add two A records:

| Type | Host | Value | TTL |
|------|------|-------|-----|
| A | `Blueprint1989` | `<server-ip>` | 3600 |
| A | `Blueprint1989api` | `<server-ip>` | 3600 |

Verify:
```bash
nslookup blueprint1989.elroitec.com
nslookup blueprint1989api.elroitec.com
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
CREATE USER "Blueprint1989" WITH PASSWORD '3lr01tec2024##';
CREATE DATABASE "Blueprint1989" OWNER "Blueprint1989";
GRANT ALL PRIVILEGES ON DATABASE "Blueprint1989" TO "Blueprint1989";
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
sudo mkdir -p /home/elroitecProjects/Blueprint1989/app
sudo mkdir -p /home/elroitecProjects/Blueprint1989/api
sudo chown -R ubuntu:ubuntu /home/elroitecProjects/Blueprint1989
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
   - Host: `localhost` � Port: `5432` � Database: `Blueprint1989` � Username: `Blueprint1989` � Password: `3lr01tec2024##`
3. **SSH** tab ? enable **Use SSH tunnel**:
   - Host: `<server-ip>` � Port: `22` � Username: `ubuntu` � Auth: Public Key (`.pem` file)
4. **Test Connection** ? **Finish**

---

## Step 4 � Build Frontend (on Windows)

### 4.1 Update `client/src/environments/environment.prod.ts`

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://blueprint1989api.elroitec.com/api',
  vapidPublicKey: 'BFEfRC079NVJZjR3LC5-a7Jajc3tFvJtjaVdq9ClWX-uLuP58nTRhnYXYuivaGiFIeq9Z2lcrKZ9hx1uOhoIyVU',
  version: '1.0.0',
  docsUrl: 'https://blueprint1989-docs.pages.dev',
};
```

> **Critical:** `vapidPublicKey` must match `Vapid:PublicKey` in `appsettings.Production.json`.

### 4.2 Build
```powershell
cd C:\dev\Blueprint1989\client
npx ng build --configuration production
```
Output: `C:\Publish\Blueprint1989\app\` (configured in `angular.json` ? `outputPath`)

### 4.3 Copy to server
```powershell
scp -r C:\Publish\Blueprint1989\app\* ubuntu@<server-ip>:/home/elroitecProjects/app/
```

---

## Step 5 � Build & Deploy Backend (on Windows)

### 5.1 Update `server/src/Blueprint1989.Api/appsettings.Production.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=Blueprint1989;Username=Blueprint1989;Password=3lr01tec2024##"
  },
  "AppSettings": {
    "AppName": "Blueprint1989",
    "PasswordExpirationDays": 30,
    "TimeZone": "AUS Eastern Standard Time",
    "AdminSignupPin": "42115"
  },
  "Jwt": {
    "Key": "Production2026SecureJwtKeyForBlueprint1989AuthenticationChangeInProductionMin32Chars3K9P",
    "MfaTempKey": "ProductionMfaTempKey2026SecureForTwoFactorAuthChangeInProductionMinimum32CharsLongRequired6W",
    "Issuer": "blueprint1989api.elroitec.com",
    "Audience": "blueprint1989api.elroitec.com",
    "ExpirationMinutes": 30
  },
  "Vapid": {
    "Subject": "mailto:elroitec@gmail.com",
    "PublicKey": "BFEfRC079NVJZjR3LC5-a7Jajc3tFvJtjaVdq9ClWX-uLuP58nTRhnYXYuivaGiFIeq9Z2lcrKZ9hx1uOhoIyVU",
    "PrivateKey": "YOUR_VAPID_PRIVATE_KEY"
  },
  "WebAuthn": {
    "RelyingPartyId": "blueprint1989.elroitec.com",
    "RelyingPartyName": "Blueprint1989",
    "Origin": "https://blueprint1989.elroitec.com"
  },
  "AllowedHosts": "blueprint1989api.elroitec.com;blueprint1989.elroitec.com;localhost;127.0.0.1",
  "Cors": {
    "AllowedOrigins": [
      "https://blueprint1989.elroitec.com",
      "https://blueprint1989api.elroitec.com"
    ]
  },
  "GoogleDrive": {
    "ClientId": "PRODUCTION_CLIENT_ID",
    "ClientSecret": "PRODUCTION_CLIENT_SECRET",
    "RedirectUri": "https://blueprint1989.elroitec.com/api/auth/google-callback"
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
| `Jwt:Issuer` + `Jwt:Audience` | `blueprint1989api.elroitec.com` |
| `Jwt:Key` | At least 32 chars, keep secret |
| `Vapid:PublicKey` | Must match `environment.prod.ts` |
| `Cors:AllowedOrigins` | Must include `https://blueprint1989.elroitec.com` |
| `AllowedHosts` | Both subdomains, semicolon-separated |
| `WebAuthn:RelyingPartyId` | Frontend domain (`blueprint1989.elroitec.com`) |

### 5.2 Publish
```powershell
cd C:\dev\Blueprint1989\server\src\Blueprint1989.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o C:\dev\Blueprint1989\publish\api
```

### 5.3 Copy to server
```powershell
scp -r C:\dev\Blueprint1989\publish\api\* ubuntu@<server-ip>:/home/elroitecProjects/Blueprint1989/api/
```

---

## Step 6 � Run Database Migrations

**Option A � From Windows (recommended for first deploy):**
```powershell
cd C:\dev\Blueprint1989\server\src\Blueprint1989.Api
$env:ConnectionStrings__DefaultConnection = "Host=<server-ip>;Port=5432;Database=Blueprint1989;Username=Blueprint1989;Password=3lr01tec2024##"
dotnet ef database update
```

**Option B � From the server:**
```bash
cd /home/elroitecProjects/Blueprint1989/api
export ASPNETCORE_ENVIRONMENT=Production
dotnet Blueprint1989.Api.dll --migrate
```

**Generate idempotent SQL script (apply only unapplied migrations):**
```powershell
cd C:\dev\Blueprint1989\server\src\Blueprint1989.Api
dotnet ef migrations script --idempotent -o migration.sql
```

---

## Step 7 � Create systemd Service

On the server:
```bash
sudo nano /etc/systemd/system/Blueprint1989-api.service
```

Paste:
```ini
[Unit]
Description=Blueprint1989 .NET API
After=network.target postgresql.service

[Service]
WorkingDirectory=/home/elroitecProjects/Blueprint1989/api
ExecStart=/usr/bin/dotnet /home/elroitecProjects/Blueprint1989/api/Blueprint1989.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=Blueprint1989-api
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
sudo systemctl enable Blueprint1989-api
sudo systemctl start Blueprint1989-api
sudo systemctl status Blueprint1989-api
```

Verify:
```bash
curl -s http://localhost:1954/api/health
```

---

## Step 8 � Configure Nginx

### 8.1 Frontend + API proxy (`blueprint1989.elroitec.com`)
```bash
sudo nano /etc/nginx/sites-available/Blueprint1989
```
```nginx
server {
    server_name blueprint1989.elroitec.com;

    # Frontend app location
    root /home/elroitecProjects/Blueprint1989/app;
    index index.html;

    # API proxy to .NET backend
    location /api/ {
        proxy_pass http://127.0.0.1:1954/api/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
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

    listen 443 ssl;
    ssl_certificate /etc/letsencrypt/live/blueprint1989.elroitec.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/blueprint1989.elroitec.com/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;
}

server {
    if ($host = blueprint1989.elroitec.com) {
        return 301 https://$host$request_uri;
    }

    listen 80;
    server_name blueprint1989.elroitec.com;
    return 404;
}
```

### 8.2 Backend (`blueprint1989api.elroitec.com` on port 1954)
```bash
sudo nano /etc/nginx/sites-available/Blueprint1989api
```
```nginx
server {
    listen 80;
    server_name blueprint1989api.elroitec.com;

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
sudo ln -s /etc/nginx/sites-available/Blueprint1989api /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Step 9 � SSL Certificates (Let's Encrypt)

```bash
sudo certbot --nginx -d blueprint1989.elroitec.com -d blueprint1989api.elroitec.com
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
sudo systemctl status Blueprint1989-api
sudo systemctl status nginx

# API on localhost
curl -s http://localhost:1954/api/health

# Frontend
curl -s -o /dev/null -w "%{http_code}" https://blueprint1989.elroitec.com:4300/

# API HTTPS
curl -s -o /dev/null -w "%{http_code}" https://blueprint1989api.elroitec.com/api/health

# Firewall
sudo ufw status

# Live API logs
sudo journalctl -u Blueprint1989-api -f
```

Open in browser:
- `https://blueprint1989.elroitec.com:4300` ? Angular login screen
- `https://blueprint1989api.elroitec.com/api/swagger` ? Swagger UI

---

## Redeployment (future updates)

### Frontend only
```powershell
cd C:\dev\Blueprint1989\client
npx ng build --configuration production
scp -r C:\Publish\Blueprint1989\app\* ubuntu@<server-ip>:/home/elroitecProjects/app/
```
No restart needed � Nginx serves static files directly.

### Backend only
```powershell
cd C:\dev\Blueprint1989\server\src\Blueprint1989.Api
dotnet publish -c Release -r linux-x64 --self-contained false -o C:\dev\Blueprint1989\publish\api
scp -r C:\dev\Blueprint1989\publish\api\* ubuntu@<server-ip>:/home/elroitecProjects/Blueprint1989/api/
```
Then on server:
```bash
sudo systemctl restart Blueprint1989-api
sudo systemctl status Blueprint1989-api
```

### Config only (no rebuild)
```bash
nano /home/elroitecProjects/Blueprint1989/api/appsettings.Production.json
sudo systemctl restart Blueprint1989-api
```

### Generate new VAPID keys
```powershell
cd C:\dev\Blueprint1989\server\vapid-keygen
dotnet run
```
Copy output into `appsettings.Production.json` (`Vapid` section) **and** `environment.prod.ts` (`vapidPublicKey`), then rebuild and redeploy both.

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| `504 Gateway Timeout` | API not running | `sudo systemctl restart Blueprint1989-api` then `sudo journalctl -u Blueprint1989-api -n 50` |
| `400 Bad Request - Invalid Hostname` | Domain missing from `AllowedHosts` | Add domain to `AllowedHosts` in `appsettings.Production.json`, restart API |
| `401 Unauthorized` on all calls | `Jwt:Issuer`/`Jwt:Audience` mismatch | Ensure both are `blueprint1989api.elroitec.com` in `appsettings.Production.json` |
| `CORS error` in browser | Frontend origin not allowed | Add `https://blueprint1989.elroitec.com` to `Cors:AllowedOrigins`, restart API |
| `styles.css 404` | Stale service worker | DevTools ? Application ? Service Workers ? Unregister ? hard refresh |
| `404` on page refresh | Missing Angular fallback | Ensure `try_files $uri $uri/ /index.html;` in Nginx config |
| DB connection error | Wrong credentials | Check `ConnectionStrings` in `appsettings.Production.json` |
| SSL certificate error | Cert expired | `sudo certbot certificates` then `sudo certbot renew` |
| API not starting | Missing runtime | `sudo journalctl -u Blueprint1989-api -n 100 --no-pager` and `dotnet --version` |
| Port not reachable | Firewall blocking | `sudo ufw allow 4300/tcp` and `sudo ufw allow 1954/tcp` |

### Useful debug commands
```bash
sudo journalctl -u Blueprint1989-api.service -n 200 --no-pager
dotnet /home/elroitecProjects/Blueprint1989/api/Blueprint1989.Api.dll
dotnet --info
sudo cat /etc/systemd/system/Blueprint1989-api.service
sudo ufw status numbered
sudo ss -tlnp | grep -E '4300|1954'
```


