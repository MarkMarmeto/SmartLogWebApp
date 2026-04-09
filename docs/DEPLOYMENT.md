# SmartLog Web App — Deployment Guide

Two deployment options are supported: **Docker** (recommended for development and simple setups) and **Windows Service** (recommended for production school environments).

---

## Prerequisites

### Hardware (Windows Service deployment)

| Component | Minimum | Recommended |
|---|---|---|
| CPU | 2 cores | 4 cores |
| RAM | 4 GB | 8 GB |
| Disk | 10 GB free | 20 GB free |
| Network | Ethernet (LAN) | Gigabit Ethernet |
| USB port | 1 (for GSM modem, if using SMS) | — |

---

## Option A: Docker (Quick Start)

### Requirements
- Docker Desktop installed

### Start

```bash
git clone <repository-url>
cd SmartLogWebApp
docker-compose up --build -d
```

Migrations run automatically on startup. Open **http://localhost:8080** when the container is healthy.

### Default Credentials

| Username | Password | Role |
|---|---|---|
| `super.admin` | `SecurePass1!` | SuperAdmin |
| `admin.amy` | `SecurePass1!` | Admin |

Change these immediately after first login.

### Common Docker Commands

```bash
# View logs
docker-compose logs -f smartlog-web

# Stop
docker-compose down

# Full reset (destroys all data)
docker-compose down -v && docker-compose up --build -d

# Check migration status (inside container)
docker exec -it smartlog-web dotnet ef migrations list

# Access SQL Server directly
docker exec -it smartlog-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "SmartLog2026!" -C
```

### Docker Services

| Service | Port | Description |
|---|---|---|
| `smartlog-web` | 8080 | Web application |
| `smartlog-db` | 1433 | SQL Server Express |

---

## Option B: Windows Service (Production)

The application runs as a **Windows Service** — starts automatically on boot, runs in the background, requires no user logged in.

### Deployment Scripts

All scripts are in the `deploy/` folder. Run `.bat` files as Administrator (they launch the corresponding `.ps1` script).

| Script | Purpose |
|---|---|
| `Setup-SmartLog.bat` | **Full automated installation wizard** — checks prereqs, creates DB, generates HMAC secret, sets env vars, builds, configures firewall, registers Windows Service |
| `Update-SmartLog.bat` | Update existing installation — backs up, pulls latest code, rebuilds, restarts service. Supports `-SkipBackup` and `-Branch` flags |
| `Setup-Network.bat` | Detect current network settings and configure a static IP address interactively |
| `Setup-Https.bat` | Generate a self-signed SSL certificate and configure HTTPS on port 5051 |
| `GenerateHMAC.bat` | Generate a cryptographically secure 256-bit HMAC secret key (Base64 output) |
| `Reset-SmartLog.bat` | Remove the installation — stops service, removes env vars, firewall rules. Optionally drops the database |
| `Backup-SmartLog.ps1` | Backup DB + app files to `C:\SmartLogBackups\`, retains last 7 daily backups. Can be registered as a Windows Scheduled Task |

**Recommended flow for a fresh installation:**

```cmd
# 1. Run the full setup wizard (handles everything)
deploy\Setup-SmartLog.bat

# 2. Optionally set up HTTPS
deploy\Setup-Https.bat

# 3. Optionally configure network / static IP
deploy\Setup-Network.bat
```

### Step 1: Install SQL Server Express

1. Download **SQL Server 2022 Express** from microsoft.com/sql-server.
2. Run installer → choose **Basic** → accept defaults.
3. Note the instance name: default is `SQLEXPRESS`.
4. Verify it is running:

```cmd
sc query MSSQL$SQLEXPRESS
```

Expected: `STATE: 4  RUNNING`

### Step 2: Create the SmartLog Database

```cmd
sqlcmd -S .\SQLEXPRESS -E -Q "CREATE DATABASE SmartLog"
```

### Step 3: Install .NET 8.0 SDK

Download from dotnet.microsoft.com/download/dotnet/8.0. Verify:

```cmd
dotnet --version
```

Expected: version starting with `8.0`.

### Step 4: Clone the Repository

```cmd
git clone <repository-url> C:\Source\SmartLogWebApp
```

### Step 5: Set Environment Variables

Open **Command Prompt as Administrator** and run each command:

```cmd
setx /M SMARTLOG_DB_CONNECTION "Server=.\SQLEXPRESS;Database=SmartLog;Trusted_Connection=true;TrustServerCertificate=true"
setx /M SMARTLOG_HMAC_SECRET_KEY "your-generated-secret-key-here"
setx /M SMARTLOG_SEED_PASSWORD "SmartLog@2026!"
setx /M ASPNETCORE_URLS "http://+:5050"
setx /M ASPNETCORE_ENVIRONMENT "Production"
```

Each command should respond: `SUCCESS: Specified value was saved.`

**Generate the HMAC secret key** (PowerShell):

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

> Keep the HMAC secret confidential. Changing it later invalidates all existing QR codes.

**Verify after opening a new Command Prompt:**

```cmd
echo %SMARTLOG_DB_CONNECTION%
echo %SMARTLOG_HMAC_SECRET_KEY%
```

### Step 6: Build and Publish

```cmd
cd C:\Source\SmartLogWebApp
dotnet build
dotnet publish src\SmartLog.Web -p:PublishProfile=WinX64 -o C:\SmartLog
```

> **Skip steps 6 and 7** if you used `Setup-SmartLog.bat` — it handles both automatically.

### Step 7: Install as Windows Service

**Option A — Automated (recommended):**

```cmd
cd C:\Source\SmartLogWebApp
deploy\Setup-SmartLog.bat
```

Run as Administrator. The wizard handles prerequisites, build, publish, service registration, and firewall in one go.

**Option B — Manual:**

```cmd
sc create SmartLogWeb binPath="C:\SmartLog\SmartLog.Web.exe" DisplayName="SmartLog Web Application" start=auto obj=LocalSystem
sc description SmartLogWeb "SmartLog School Information Management System"
sc failure SmartLogWeb reset=86400 actions=restart/5000/restart/10000/restart/30000
sc start SmartLogWeb
```

### Step 8: Open Firewall Port

```cmd
netsh advfirewall firewall add rule name="SmartLog Web" dir=in action=allow protocol=TCP localport=5050
```

### Step 9: Verify

```cmd
sc query SmartLogWeb
curl http://localhost:5050/health
```

Expected: `STATE: 4  RUNNING` and response `Healthy`.

Open **http://localhost:5050** — you should see the SmartLog login page.

---

## First-Time Application Setup

On first startup, SmartLog automatically applies migrations and seeds initial data.

### 1. Log In

Open `http://localhost:5050` (Windows) or `http://localhost:8080` (Docker). Log in as `super.admin` with your `SMARTLOG_SEED_PASSWORD`.

### 2. Change Default Passwords

Navigate to **Admin > Manage Users** and change passwords for all seeded accounts:

| Username | Role |
|---|---|
| `super.admin` | SuperAdmin |
| `admin.amy` | Admin |
| `teacher.tina` | Teacher |
| `guard.gary` | Security |
| `staff.sarah` | Staff |

Deactivate accounts you don't need.

### 3. Configure Academic Year

Navigate to **Admin > Academic Years**. Verify the current year's dates and mark it as **Current**.

### 4. Set Up Grade Levels and Sections

- **Admin > Grade Levels** — grades 7–12 are pre-loaded
- **Admin > Sections** — sections A, B, C pre-loaded (capacity 40 each)

Add, rename, or remove sections to match your school.

### 5. Add Students

Navigate to **Admin > Students**. Add individually or use **Bulk Import** (CSV).

### 6. Generate QR Codes

Navigate to **Admin > QR Codes**. Select students and click **Generate QR Code**. Print and attach to student ID cards.

### 7. Configure Calendar

Navigate to **Admin > Calendar**. 13 Philippine national holidays are pre-loaded. Add school-specific events, suspensions, or additional holidays.

### 8. Register Scanner Devices

Navigate to **Admin > Register Device**. Enter a device name and location. The API key (`sk_live_xxx`) is shown **once** — copy it immediately and enter it in the SmartLog Scanner App.

---

## SMS Configuration

### GSM Modem (Offline-Capable, Recommended)

1. Insert SIM card into USB GSM modem and plug into the server.
2. Open **Device Manager** and note the COM port (e.g., `COM3`).
3. In SmartLog: **Admin > Settings > SMS**:

| Setting | Value |
|---|---|
| SMS Enabled | `true` |
| Default Provider | `GSM_MODEM` |
| GSM Modem Port Name | `COM3` |
| GSM Modem Baud Rate | `9600` |
| GSM Modem Send Delay (ms) | `3000` |

4. Click **Save**, then use **Test SMS** to verify.

### Semaphore (Cloud, Internet Required)

1. Create an account at semaphore.co and get your API key.
2. In SmartLog: **Admin > Settings > SMS**:

| Setting | Value |
|---|---|
| SMS Enabled | `true` |
| Default Provider | `SEMAPHORE` |
| Semaphore API Key | Your API key |
| Semaphore Sender Name | `SmartLog` |

### Fallback Configuration

Set **Default Provider** to `GSM_MODEM` and **Fallback Enabled** to `true`. SmartLog will automatically retry via Semaphore if the GSM modem fails.

---

## Network Setup for Scanner Devices

### Overview

```
School LAN / Wi-Fi (e.g., 192.168.1.x)

  SmartLog Server (static IP: 192.168.1.10, port: 5050)
       │
       ├── Scanner Device #1 (Main Gate)
       └── Scanner Device #2 (Back Gate)
```

All scanner devices and the server must be on the **same network**. The server requires a **static IP** so scanner devices always know where to find it.

### Set a Static IP on the Server

**Via Windows Settings:**

1. Settings > Network & Internet > Ethernet (or Wi-Fi) > IP assignment > Edit
2. Switch from Automatic (DHCP) to **Manual**
3. Set IPv4:

| Field | Example |
|---|---|
| IP address | `192.168.1.10` (choose one outside the router's DHCP range) |
| Subnet prefix length | `24` |
| Gateway | `192.168.1.1` (your router) |
| Preferred DNS | `192.168.1.1` |

**Via Command Prompt (Admin):**

```cmd
netsh interface ip set address "Ethernet" static 192.168.1.10 255.255.255.0 192.168.1.1
netsh interface ip set dns "Ethernet" static 192.168.1.1
```

### Test Connectivity from Scanner Device

On the scanner device (same LAN):

```cmd
ping 192.168.1.10
curl http://192.168.1.10:5050/health
```

Expected: ping replies + `Healthy` response.

### Changing the Port

```cmd
setx /M ASPNETCORE_URLS "http://+:8080"
sc stop SmartLogWeb & timeout /t 3 & sc start SmartLogWeb
netsh advfirewall firewall add rule name="SmartLog Web" dir=in action=allow protocol=TCP localport=8080
```

Update the Server URL on all scanner devices after changing the port.

---

## Managing the Windows Service

```cmd
# Check status
sc query SmartLogWeb

# Start / Stop / Restart
sc start SmartLogWeb
sc stop SmartLogWeb
sc stop SmartLogWeb & timeout /t 5 & sc start SmartLogWeb

# View logs (PowerShell)
Get-Content "C:\SmartLog\logs\*.log" -Tail 50
```

---

## Updating SmartLog (Windows Service)

**Option A — Update script (recommended):**

```cmd
deploy\Update-SmartLog.bat
```

Backs up the current install, pulls latest code, rebuilds, and restarts the service. Supports optional flags when run directly via PowerShell:

```powershell
# Skip backup (faster, use when you have a recent manual backup)
.\deploy\Update-SmartLog.ps1 -SkipBackup

# Pull from a specific branch
.\deploy\Update-SmartLog.ps1 -Branch staging
```

**Option B — Manual:**

```cmd
cd C:\Source\SmartLogWebApp
git pull
sc stop SmartLogWeb
dotnet publish src\SmartLog.Web -p:PublishProfile=WinX64 -o C:\SmartLog
sc start SmartLogWeb
```

---

## Backup & Restore

### Automated Backup (Recommended)

Use `deploy\Backup-SmartLog.ps1` — backs up both the database and app files, retains the last 7 daily backups in `C:\SmartLogBackups\`.

```powershell
# Run manually
.\deploy\Backup-SmartLog.ps1

# Register as a daily scheduled task (run once, as Administrator)
.\deploy\Backup-SmartLog.ps1 -RegisterTask
```

### Manual Database Backup

```cmd
sqlcmd -S .\SQLEXPRESS -E -Q "BACKUP DATABASE SmartLog TO DISK='C:\SmartLogBackups\SmartLog.bak' WITH FORMAT"
```

### Restore

```cmd
sc stop SmartLogWeb
sqlcmd -S .\SQLEXPRESS -E -Q "RESTORE DATABASE SmartLog FROM DISK='C:\SmartLogBackups\SmartLog.bak' WITH REPLACE"
sc start SmartLogWeb
```

---

## Resetting the Installation

To wipe the installation and start fresh (e.g., reconfiguring for a different server):

```cmd
deploy\Reset-SmartLog.bat
```

This stops and removes the Windows Service, clears environment variables, and removes firewall rules. Optionally drops the database. Does **not** delete source code or backups.

After reset, run `deploy\Setup-SmartLog.bat` to reinstall.

---

## Troubleshooting

| Problem | Check |
|---|---|
| Login page not loading | `sc query SmartLogWeb` — must show RUNNING. Check `%ASPNETCORE_URLS%`. |
| `Healthy` endpoint not responding | Firewall rule for port 5050. Re-run `netsh advfirewall` command. |
| Scanner can't connect | Static IP set on server? Firewall open? Run `ping` and `curl /health` from scanner device. |
| "Connection string" error on startup | Verify `%SMARTLOG_DB_CONNECTION%` and that SQL Server service is running. |
| SMS not sending | Check GSM modem COM port in Device Manager. Use **Test SMS** in Admin > Settings. |
| Database migration error | Check application logs. Run `sc stop SmartLogWeb`, fix the issue, then `sc start SmartLogWeb`. |
