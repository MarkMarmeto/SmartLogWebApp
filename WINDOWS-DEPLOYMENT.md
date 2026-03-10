# SmartLog Web Application — Windows Setup & Deployment Guide

Complete step-by-step guide for setting up and deploying SmartLog natively on a Windows machine. The application runs as a **Windows Service** — it starts automatically on boot, runs in the background, and requires no user to be logged in.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Step 1: Install SQL Server Express](#2-step-1-install-sql-server-express)
3. [Step 2: Create the SmartLog Database](#3-step-2-create-the-smartlog-database)
4. [Step 3: Install .NET 8.0 SDK](#4-step-3-install-net-80-sdk)
5. [Step 4: Install Git and Clone the Repository](#5-step-4-install-git-and-clone-the-repository)
6. [Step 5: Set Environment Variables](#6-step-5-set-environment-variables)
7. [Step 6: Build and Publish the Application](#7-step-6-build-and-publish-the-application)
8. [Step 7: Install as a Windows Service](#8-step-7-install-as-a-windows-service)
9. [Step 8: Open the Firewall Port](#9-step-8-open-the-firewall-port)
10. [Step 9: Verify the Deployment](#10-step-9-verify-the-deployment)
11. [First-Time Application Setup](#11-first-time-application-setup)
12. [SMS Configuration](#12-sms-configuration)
13. [Network Setup for Scanner Devices](#13-network-setup-for-scanner-devices) — Static IP, LAN/Wi-Fi, connectivity testing, scanner registration
14. [Network Configuration (Additional)](#14-network-configuration-additional) — Changing ports, typical school router setup, troubleshooting
15. [Managing the Service](#15-managing-the-service)
16. [Backup & Restore](#16-backup--restore)
17. [Updating SmartLog](#17-updating-smartlog)
18. [Troubleshooting](#18-troubleshooting)
19. [Uninstalling](#19-uninstalling)
20. [Quick Reference](#20-quick-reference)

---

## 1. Prerequisites

### Hardware Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| CPU | 2 cores | 4 cores |
| RAM | 4 GB | 8 GB |
| Disk | 10 GB free | 20 GB free |
| Network | Ethernet (LAN) | Gigabit Ethernet |
| USB port | 1 (for GSM modem, if using SMS) | — |

### Software to Install

You will install these in the steps below:

| # | Software | Purpose |
|---|----------|---------|
| 1 | SQL Server 2022 Express | Database engine |
| 2 | .NET 8.0 SDK | Build the application from source |
| 3 | Git for Windows | Download the source code |

> **Note:** After publishing, the output is a **self-contained executable** (`SmartLog.Web.exe`). The .NET SDK is only needed on the machine where you build. If you build on one machine and copy the published files to another, the target machine does **not** need .NET installed.

---

## 2. Step 1: Install SQL Server Express

### 2.1 Download

Go to https://www.microsoft.com/en-us/sql-server/sql-server-downloads and download **SQL Server 2022 Express**.

### 2.2 Install

1. Run the downloaded installer.
2. Choose **Basic** installation type.
3. Accept the license terms.
4. Leave the install location as default (or change it if needed).
5. Click **Install** and wait for the installation to complete.

### 2.3 Note the Instance Name

When installation finishes, the summary screen will show the instance name. The default is:

```
SQLEXPRESS
```

The connection string for this instance is:

```
Server=.\SQLEXPRESS
```

Keep this window open or write this down — you will need it in Step 5.

### 2.4 Verify SQL Server is Running

Open **Command Prompt** and run:

```cmd
sc query MSSQL$SQLEXPRESS
```

You should see `STATE: 4  RUNNING`. If it shows `STOPPED`, start it:

```cmd
net start MSSQL$SQLEXPRESS
```

### 2.5 Install SQL Server Command Line Tools (Optional but Recommended)

Download and install **sqlcmd** from:
https://learn.microsoft.com/en-us/sql/tools/sqlcmd/sqlcmd-utility

This gives you the `sqlcmd` command used throughout this guide for database management.

---

## 3. Step 2: Create the SmartLog Database

### Option A: Using sqlcmd (Command Line)

Open **Command Prompt** and run:

```cmd
sqlcmd -S .\SQLEXPRESS -E -Q "CREATE DATABASE SmartLog"
```

You should see:

```
Commands completed successfully.
```

### Option B: Using SQL Server Management Studio (SSMS)

1. Download SSMS from https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms.
2. Install and open SSMS.
3. Connect to `.\SQLEXPRESS` using **Windows Authentication**.
4. Right-click **Databases** in the left panel.
5. Click **New Database...**
6. Enter `SmartLog` as the database name.
7. Click **OK**.

### Verify the Database Exists

```cmd
sqlcmd -S .\SQLEXPRESS -E -Q "SELECT name FROM sys.databases WHERE name = 'SmartLog'"
```

Expected output:

```
name
----------------------------------------------------------------
SmartLog
```

---

## 4. Step 3: Install .NET 8.0 SDK

### 4.1 Download

Go to https://dotnet.microsoft.com/download/dotnet/8.0 and download the **.NET 8.0 SDK** installer for Windows x64.

### 4.2 Install

1. Run the downloaded installer.
2. Follow the prompts and complete the installation.

### 4.3 Verify

Close and reopen **Command Prompt**, then run:

```cmd
dotnet --version
```

You should see a version number starting with `8.0` (e.g., `8.0.xxx`).

---

## 5. Step 4: Install Git and Clone the Repository

### 5.1 Install Git

1. Download Git from https://git-scm.com/download/win.
2. Run the installer.
3. Use the default options throughout the wizard.
4. Click **Install**.

### 5.2 Clone the Repository

Open **Command Prompt** and run:

```cmd
git clone <repository-url> C:\Source\SmartLogWebApp
```

> Replace `<repository-url>` with the actual Git repository URL provided to you.

### 5.3 Verify

```cmd
dir C:\Source\SmartLogWebApp\src\SmartLog.Web
```

You should see files including `SmartLog.Web.csproj`, `Program.cs`, `appsettings.json`, etc.

---

## 6. Step 5: Set Environment Variables

SmartLog reads its configuration from **system-wide environment variables**. These must be set before the service can start.

### 6.1 Open Command Prompt as Administrator

1. Click the **Start** menu.
2. Type `cmd`.
3. Right-click **Command Prompt** and select **Run as administrator**.
4. Click **Yes** on the UAC prompt.

### 6.2 Set the Required Variables

Run each of the following commands:

**Database connection string:**

```cmd
setx /M SMARTLOG_DB_CONNECTION "Server=.\SQLEXPRESS;Database=SmartLog;Trusted_Connection=true;TrustServerCertificate=true"
```

**HMAC secret key for QR code signing (see 6.3 to generate):**

```cmd
setx /M SMARTLOG_HMAC_SECRET_KEY "your-generated-secret-key-here"
```

**Initial admin password (must be 8+ chars with uppercase, lowercase, digit, and special character):**

```cmd
setx /M SMARTLOG_SEED_PASSWORD "SmartLog@2026!"
```

**URL the application will listen on:**

```cmd
setx /M ASPNETCORE_URLS "http://+:5050"
```

**Set production mode:**

```cmd
setx /M ASPNETCORE_ENVIRONMENT "Production"
```

Each command should respond with:

```
SUCCESS: Specified value was saved.
```

### 6.3 Generate the HMAC Secret Key

The HMAC key is used to sign student QR codes. It must be a strong, random string. Generate one using PowerShell:

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

This outputs a random Base64 string like `a7Bx9kL2mN4pQ6rS8tU0vW2xY4zA6bC8dE0fG2hI4j=`. Copy the output and use it in the `setx /M SMARTLOG_HMAC_SECRET_KEY` command above.

> **Important:** Keep this key secret. If it is changed later, all previously generated QR codes will become invalid.

### 6.4 Verify the Variables are Set

**Close your current Command Prompt and open a new one** (environment variables set with `setx /M` require a new session), then run:

```cmd
echo %SMARTLOG_DB_CONNECTION%
echo %SMARTLOG_HMAC_SECRET_KEY%
echo %SMARTLOG_SEED_PASSWORD%
echo %ASPNETCORE_URLS%
echo %ASPNETCORE_ENVIRONMENT%
```

Each should print the value you set. If any prints the variable name instead (e.g., `%SMARTLOG_DB_CONNECTION%`), the variable was not set correctly — re-run the `setx /M` command for that variable.

### 6.5 What Each Variable Does

| Variable | Purpose |
|----------|---------|
| `SMARTLOG_DB_CONNECTION` | Connection string to the SQL Server database. Uses Windows Authentication (`Trusted_Connection=true`) so no SQL password is needed. |
| `SMARTLOG_HMAC_SECRET_KEY` | Secret key used to sign and verify student QR codes (HMAC-SHA256). Must be kept confidential. |
| `SMARTLOG_SEED_PASSWORD` | Password assigned to all default admin accounts on first startup. You will change these after first login. |
| `ASPNETCORE_URLS` | The URL and port the application listens on. `http://+:5050` means listen on port 5050 on all network interfaces. |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production` for deployment. Controls error pages, HTTPS enforcement, and logging levels. |

---

## 7. Step 6: Build and Publish the Application

### 7.1 Build (Verify No Errors)

Open **Command Prompt** (a new window, so it picks up the environment variables) and run:

```cmd
cd C:\Source\SmartLogWebApp
dotnet build
```

Wait for it to finish. You should see:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If there are errors, check that the .NET 8.0 SDK is installed correctly (`dotnet --version`).

### 7.2 Run Tests (Optional but Recommended)

```cmd
dotnet test
```

You should see:

```
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19
```

### 7.3 Publish

Publish the application as a self-contained Windows executable:

```cmd
dotnet publish src\SmartLog.Web -p:PublishProfile=WinX64 -o C:\SmartLog
```

This creates `C:\SmartLog\SmartLog.Web.exe` along with all required files. The output is self-contained — it includes the .NET runtime, so the target machine does not need .NET installed separately.

### 7.4 Verify the Published Files

```cmd
dir C:\SmartLog\SmartLog.Web.exe
```

You should see the `SmartLog.Web.exe` file.

---

## 8. Step 7: Install as a Windows Service

### Option A: Use the Installer Script (Recommended)

The repository includes an installer script that handles everything:

```cmd
cd C:\Source\SmartLogWebApp
deploy\install-windows-service.bat
```

> **This script must be run as Administrator.** If you didn't open your Command Prompt as admin, right-click the `.bat` file in File Explorer and select **Run as administrator**.

The script will:
1. Publish the application to `C:\SmartLog`.
2. Remove any existing SmartLogWeb service.
3. Create a new Windows Service named **SmartLogWeb**.
4. Set it to start automatically on boot.
5. Configure automatic restart on failure (after 5s, 10s, and 30s).
6. Start the service.

Skip to [Step 8 (Firewall)](#9-step-8-open-the-firewall-port) after the script finishes successfully.

### Option B: Manual Installation

If you prefer to run the commands yourself, open **Command Prompt as Administrator**:

**Create the service:**

```cmd
sc create SmartLogWeb binPath="C:\SmartLog\SmartLog.Web.exe" DisplayName="SmartLog Web Application" start=auto obj=LocalSystem
```

**Add a description:**

```cmd
sc description SmartLogWeb "SmartLog School Information Management System - Attendance tracking, SMS notifications, and administrative tools."
```

**Configure automatic restart on failure:**

```cmd
sc failure SmartLogWeb reset=86400 actions=restart/5000/restart/10000/restart/30000
```

This tells Windows: if the service crashes, restart it after 5 seconds on the first failure, 10 seconds on the second, and 30 seconds on subsequent failures. The failure counter resets every 24 hours.

**Start the service:**

```cmd
sc start SmartLogWeb
```

---

## 9. Step 8: Open the Firewall Port

For other devices on the LAN (scanner devices, other computers) to access SmartLog, you need to allow incoming connections on port 5050.

Open **Command Prompt as Administrator** and run:

```cmd
netsh advfirewall firewall add rule name="SmartLog Web" dir=in action=allow protocol=TCP localport=5050
```

You should see:

```
Ok.
```

> **Note:** If you only need to access SmartLog from the same machine (localhost), you can skip this step.

---

## 10. Step 9: Verify the Deployment

### 10.1 Check the Service is Running

```cmd
sc query SmartLogWeb
```

You should see:

```
STATE              : 4  RUNNING
```

### 10.2 Check the Health Endpoint

```cmd
curl http://localhost:5050/health
```

Expected response:

```
Healthy
```

> If `curl` is not available, open a browser and navigate to `http://localhost:5050/health`. It should display `Healthy`.

### 10.3 Open the Application

Open a web browser and go to:

```
http://localhost:5050
```

You should see the **SmartLog login page**. If you see this, the deployment is complete.

---

## 11. First-Time Application Setup

On first startup, SmartLog automatically applies database migrations (creates all tables) and seeds initial data (admin accounts, grade levels, holidays, etc.).

### 11.1 Log In as Super Admin

1. Open `http://localhost:5050` in your browser.
2. Log in with:
   - **Username:** `super.admin`
   - **Password:** The value you set for `SMARTLOG_SEED_PASSWORD` (e.g., `SmartLog@2026!`)

### 11.2 Change Default Passwords

**This is critical — do this immediately.** Navigate to **Admin > Manage Users**.

The following accounts are created automatically with the seed password:

| Username | Role | Purpose |
|----------|------|---------|
| `super.admin` | SuperAdmin | Full system access, manages all settings and users |
| `admin.amy` | Admin | Day-to-day administration, student and faculty management |
| `teacher.tina` | Teacher | View attendance, view student records |
| `guard.gary` | Security | Gate scanning operations, view attendance |
| `staff.sarah` | Staff | View student records |

Change the password for each account you plan to use. Deactivate any accounts you don't need.

### 11.3 Configure Academic Year

1. Navigate to **Admin > Academic Years**.
2. Verify the current academic year is correct (e.g., `2025-2026`).
3. Adjust the start and end dates to match your school calendar.
4. Ensure the correct year is marked as **Current**.

### 11.4 Set Up Grade Levels and Sections

1. Navigate to **Admin > Grade Levels** — grades 7-12 are pre-loaded.
2. Navigate to **Admin > Sections** — sections A, B, C are pre-loaded for each grade (capacity: 40).
3. Add, rename, or remove sections to match your school.
4. Assign section advisers if applicable.

### 11.5 Add Students

1. Navigate to **Admin > Students**.
2. Add students individually by clicking **Add Student** and filling in:
   - Full name
   - LRN (Learner Reference Number)
   - Grade level and section
   - Parent/guardian mobile number (for SMS notifications)
3. Or use **Bulk Import** to upload a CSV file of student records.
4. Each student is automatically assigned a Student ID in the format `YYYY-GG-NNNN` (e.g., `2026-07-0001`).

### 11.6 Generate QR Codes

1. Navigate to **Admin > QR Codes**.
2. Select students and click **Generate QR Code**.
3. Each QR code contains a signed payload: `SMARTLOG:{studentId}:{timestamp}:{hmac}`.
4. Print the QR codes and attach them to student ID cards.

### 11.7 Configure Calendar

1. Navigate to **Admin > Calendar**.
2. 13 Philippine national holidays are pre-loaded for the current academic year:
   - New Year's Day, EDSA People Power, Araw ng Kagitingan, Maundy Thursday, Good Friday, Labor Day, Independence Day, Ninoy Aquino Day, National Heroes Day, All Saints' Day, Bonifacio Day, Christmas Day, Rizal Day.
3. Add school-specific events, suspension days, or additional holidays.
4. Days marked with **"Affects Attendance"** will automatically reject scans on those dates.

### 11.8 Register Scanner Devices

See [Section 13: Scanner Device Setup](#13-scanner-device-setup).

---

## 12. SMS Configuration

SmartLog sends automated SMS notifications to parents/guardians when students scan in (entry) or out (exit). Two SMS gateways are supported.

### 12.1 GSM Modem (Default — Offline-Capable)

This is the default and recommended provider for schools without reliable internet. It uses a USB GSM modem with a prepaid SIM card to send SMS directly.

**Hardware needed:**
- USB GSM modem (e.g., Huawei E173, ZTE MF190, or any AT-command-compatible modem)
- Prepaid SIM card with SMS credits/load

**Setup:**

1. Insert the SIM card into the GSM modem.
2. Plug the GSM modem into the server's USB port.
3. Open **Device Manager** (right-click Start > Device Manager).
4. Expand **Ports (COM & LPT)**.
5. Note the COM port assigned to the modem (e.g., `COM3`).
6. Open SmartLog in your browser and navigate to **Admin > Settings**.
7. Go to the **SMS** tab and configure:

| Setting | Value |
|---------|-------|
| SMS Enabled | `true` |
| Default Provider | `GSM_MODEM` |
| GSM Modem Port Name | `COM3` (match Device Manager) |
| GSM Modem Baud Rate | `9600` |
| GSM Modem Send Delay (ms) | `3000` |

8. Click **Save**.
9. Use the **Test SMS** button to send a test message to your phone.

> **Tip:** Make sure the SIM card has sufficient load/credits. The modem sends approximately 1 SMS per 3 seconds due to the send delay.

### 12.2 Semaphore (Cloud Gateway — Internet Required)

For schools with internet access, [Semaphore](https://semaphore.co) provides a cloud-based SMS API for the Philippines.

**Setup:**

1. Create an account at https://semaphore.co.
2. Purchase SMS credits and obtain your **API key** from the dashboard.
3. In SmartLog, navigate to **Admin > Settings > SMS**.
4. Configure:

| Setting | Value |
|---------|-------|
| SMS Enabled | `true` |
| Default Provider | `SEMAPHORE` |
| Semaphore API Key | Your API key from Semaphore |
| Semaphore Sender Name | `SmartLog` (or your school name) |

5. Click **Save**.
6. Use the **Test SMS** button to verify.

### 12.3 Fallback Configuration

You can configure SmartLog to automatically fall back to Semaphore when the GSM modem fails (e.g., modem disconnected, SIM out of load):

1. Set up both GSM Modem (Section 12.1) and Semaphore (Section 12.2).
2. Set **Default Provider** to `GSM_MODEM`.
3. Set **Fallback Enabled** to `true`.

SmartLog will try the GSM modem first. If it fails, it automatically retries via Semaphore.

### 12.4 SMS Templates

SmartLog comes with bilingual SMS templates (English and Filipino). Navigate to **Admin > Settings > SMS Templates** to view or customize them:

| Template Code | Purpose |
|---------------|---------|
| `ENTRY` | Sent when a student scans in |
| `EXIT` | Sent when a student scans out |
| `HOLIDAY` | Holiday announcement |
| `SUSPENSION` | Class suspension notice |
| `EMERGENCY` | Emergency alert |

Each student can be assigned a preferred SMS language (English or Filipino) in their profile.

### 12.5 How SMS Processing Works

1. A student scans their QR code at the gate.
2. SmartLog accepts the scan and adds an SMS to the **queue** (status: Pending).
3. The **SMS Worker** (background service) polls the queue every 5 seconds.
4. It sends the SMS via the configured gateway (GSM Modem or Semaphore).
5. On failure, it retries with exponential backoff: 2 minutes, 4 minutes, 8 minutes (max 3 retries).

---

## 13. Network Setup for Scanner Devices

This section explains how to set up the network so that scanner devices (laptops, tablets, or desktops running SmartLogScannerApp) can communicate with the SmartLog server over your school's LAN or Wi-Fi.

### 13.1 How It Works (Overview)

```
┌──────────────────────────────────────────────────────────────────┐
│                     School LAN / Wi-Fi Network                   │
│                        (e.g., 192.168.1.x)                       │
│                                                                  │
│   ┌─────────────────────┐         ┌──────────────────────────┐   │
│   │   SmartLog Server    │         │   Scanner Device          │   │
│   │   (Windows PC)       │         │   (Laptop at school gate) │   │
│   │                      │         │                           │   │
│   │   Static IP:         │◄───────▶│   Connects via LAN       │   │
│   │   192.168.1.10       │  HTTP   │   or Wi-Fi               │   │
│   │   Port: 5050         │         │                           │   │
│   │                      │         │   Server URL:             │   │
│   │   SmartLog.Web.exe   │         │   http://192.168.1.10:5050│   │
│   └─────────────────────┘         └──────────────────────────┘   │
│              │                                                    │
│              │ USB                  ┌──────────────────────────┐   │
│              │                      │   Scanner Device #2      │   │
│        ┌─────────┐                 │   (Another gate)         │   │
│        │GSM Modem│                 │   http://192.168.1.10:5050│   │
│        │(for SMS)│                 └──────────────────────────┘   │
│        └─────────┘                                                │
└──────────────────────────────────────────────────────────────────┘
```

The scanner device and the server must be on the **same network** (connected to the same router/switch, either via Ethernet cable or Wi-Fi). The server needs a **static IP address** so the scanner always knows where to find it.

### 13.2 Step 1: Find Your Server's Current IP Address

On the **SmartLog server** (Windows PC), open **Command Prompt** and run:

```cmd
ipconfig
```

Look for the **IPv4 Address** under your active network adapter:

**If connected via Ethernet cable:**
```
Ethernet adapter Ethernet:
   Connection-specific DNS Suffix  . :
   IPv4 Address. . . . . . . . . . . : 192.168.1.10
   Subnet Mask . . . . . . . . . . . : 255.255.255.0
   Default Gateway . . . . . . . . . : 192.168.1.1
```

**If connected via Wi-Fi:**
```
Wireless LAN adapter Wi-Fi:
   Connection-specific DNS Suffix  . :
   IPv4 Address. . . . . . . . . . . : 192.168.1.10
   Subnet Mask . . . . . . . . . . . : 255.255.255.0
   Default Gateway . . . . . . . . . : 192.168.1.1
```

Write down these three values:
- **IPv4 Address** (e.g., `192.168.1.10`) — this is your server's current IP
- **Subnet Mask** (e.g., `255.255.255.0`)
- **Default Gateway** (e.g., `192.168.1.1`) — this is your router's IP

### 13.3 Step 2: Set a Static IP on the Server

By default, your router assigns IP addresses automatically using DHCP. This means the server's IP can change every time it restarts — which would break the scanner's connection. Setting a **static IP** fixes the address permanently.

#### Option A: Using Windows Settings (Recommended)

1. Press `Win + I` to open **Settings**.
2. Go to **Network & Internet**.
3. Click **Ethernet** (if using cable) or **Wi-Fi** (if using wireless).
4. Click your active network connection (e.g., "Ethernet" or your Wi-Fi network name).
5. Scroll down to **IP assignment** and click **Edit**.
6. Change from **Automatic (DHCP)** to **Manual**.
7. Toggle **IPv4** to **On**.
8. Fill in the following:

| Field | What to Enter | Example |
|-------|---------------|---------|
| IP address | Pick an address outside your router's DHCP range (see tip below) | `192.168.1.10` |
| Subnet prefix length | Usually `24` (same as subnet mask `255.255.255.0`) | `24` |
| Gateway | Your router's IP (the Default Gateway from step 13.2) | `192.168.1.1` |
| Preferred DNS | Same as gateway, or use Google DNS | `192.168.1.1` |
| Alternate DNS | Optional backup DNS | `8.8.8.8` |

9. Click **Save**.

> **Tip — Choosing a static IP:** Most home/school routers assign DHCP addresses starting from `192.168.1.100` or `192.168.1.50`. Pick a low number like `192.168.1.10` or `192.168.1.20` to avoid conflicts. Check your router's admin page (usually at `http://192.168.1.1`) to see the DHCP range.

#### Option B: Using Control Panel (Windows 10/11)

1. Press `Win + R`, type `ncpa.cpl`, press Enter. This opens **Network Connections**.
2. Right-click your active adapter (**Ethernet** or **Wi-Fi**) and select **Properties**.
3. Select **Internet Protocol Version 4 (TCP/IPv4)** and click **Properties**.
4. Select **Use the following IP address**.
5. Enter:

| Field | Example |
|-------|---------|
| IP address | `192.168.1.10` |
| Subnet mask | `255.255.255.0` |
| Default gateway | `192.168.1.1` |

6. Select **Use the following DNS server addresses**.
7. Enter:

| Field | Example |
|-------|---------|
| Preferred DNS server | `192.168.1.1` |
| Alternate DNS server | `8.8.8.8` |

8. Click **OK**, then **Close**.

#### Option C: Using Command Prompt

Open **Command Prompt as Administrator**:

```cmd
REM For Ethernet connection:
netsh interface ip set address "Ethernet" static 192.168.1.10 255.255.255.0 192.168.1.1
netsh interface ip set dns "Ethernet" static 192.168.1.1
netsh interface ip add dns "Ethernet" 8.8.8.8 index=2

REM For Wi-Fi connection:
netsh interface ip set address "Wi-Fi" static 192.168.1.10 255.255.255.0 192.168.1.1
netsh interface ip set dns "Wi-Fi" static 192.168.1.1
netsh interface ip add dns "Wi-Fi" 8.8.8.8 index=2
```

> **Note:** Replace `"Ethernet"` or `"Wi-Fi"` with the exact adapter name shown in `ipconfig`. Replace the IP addresses with your actual network values.

### 13.4 Step 3: Verify the Static IP

After setting the static IP, verify it stuck:

```cmd
ipconfig
```

Confirm the IPv4 Address shows your chosen static IP (e.g., `192.168.1.10`).

Then verify you still have network/internet access:

```cmd
ping 192.168.1.1
```

You should see replies from your router. If you get "Request timed out", double-check the gateway address.

### 13.5 Step 4: Verify the Firewall is Open

Make sure you completed [Step 8 (Firewall)](#9-step-8-open-the-firewall-port). Verify the rule exists:

```cmd
netsh advfirewall firewall show rule name="SmartLog Web"
```

If not found, create it:

```cmd
netsh advfirewall firewall add rule name="SmartLog Web" dir=in action=allow protocol=TCP localport=5050
```

### 13.6 Step 5: Test Connectivity from the Scanner Device

On the **scanner device** (laptop/tablet at the school gate), verify it can reach the server.

**Make sure the scanner device is connected to the same network** (same Wi-Fi, or plugged into the same router/switch via Ethernet).

Open **Command Prompt** on the scanner device and run:

```cmd
REM Test basic network connectivity
ping 192.168.1.10

REM Test SmartLog is reachable on port 5050
curl http://192.168.1.10:5050/health
```

**Expected results:**

- `ping` should show replies (e.g., `Reply from 192.168.1.10: bytes=32 time<1ms TTL=128`)
- `curl` should return `Healthy`

**If ping fails:**
- Verify both devices are on the same network/subnet.
- Check Windows Firewall on the server (Step 8).
- Try temporarily disabling Windows Firewall on the server to test: `netsh advfirewall set allprofiles state off` (re-enable after: `netsh advfirewall set allprofiles state on`).

**If ping works but curl fails:**
- The SmartLog service may not be running: `sc query SmartLogWeb` on the server.
- The firewall rule may not include port 5050: re-run the firewall command from Step 8.
- The port may be wrong: `echo %ASPNETCORE_URLS%` on the server.

### 13.7 Step 6: Register the Scanner Device in SmartLog

1. On any computer, open a browser and go to `http://192.168.1.10:5050` (or `http://localhost:5050` if on the server).
2. Log in as an Admin or SuperAdmin.
3. Navigate to **Admin > Register Device**.
4. Enter:
   - **Device Name:** e.g., "Main Gate Scanner"
   - **Location:** e.g., "Main Entrance"
5. Click **Register**.
6. An API key will be displayed (format: `sk_live_xxxxxxxxxxxx`).
7. **Copy the API key immediately** — it is shown only once and cannot be retrieved later.

> **Security:** The API key is stored as a SHA-256 hash in the database. SmartLog never stores the plain-text key. If you lose it, you must register a new device.

### 13.8 Step 7: Configure the Scanner App

On the scanner device, open **SmartLogScannerApp** and enter the connection settings:

| Setting | Value | Example |
|---------|-------|---------|
| Server URL | `http://<server-static-ip>:5050` | `http://192.168.1.10:5050` |
| API Key | The key from step 13.7 | `sk_live_xxxxxxxxxxxx` |

The scanner app should now be able to connect and submit scans.

### 13.9 Step 8: Test a Scan

1. Generate a QR code for a test student (Admin > QR Codes).
2. Print or display the QR code on screen.
3. Scan the QR code with the scanner device.
4. The scanner should display the student's name, grade, and section with status `ACCEPTED`.
5. Check **Admin > Attendance** in SmartLog to verify the scan was recorded.

### 13.10 Multiple Scanner Devices

You can register as many scanner devices as needed (e.g., one per gate). Each device gets its own API key but all connect to the same server URL.

| Device | Location | Server URL |
|--------|----------|------------|
| Main Gate Scanner | Main Entrance | `http://192.168.1.10:5050` |
| Back Gate Scanner | Back Entrance | `http://192.168.1.10:5050` |
| Faculty Scanner | Faculty Room | `http://192.168.1.10:5050` |

All scanners point to the same SmartLog server.

### 13.11 Wi-Fi vs. Ethernet — Which to Use

| | Ethernet (Cable) | Wi-Fi (Wireless) |
|---|---|---|
| **Reliability** | Very reliable, no signal issues | Can drop signal, interference possible |
| **Speed** | Fast and consistent | Varies with distance and obstacles |
| **Best for** | Server, fixed scanner stations | Portable/temporary scanner stations |
| **Recommendation** | Use for the **server** and any **permanent** scanner stations | Use for **mobile** scanner stations or where cabling isn't practical |

> **Recommendation:** Connect the SmartLog server via **Ethernet cable** for maximum reliability. Scanner devices can use either Ethernet or Wi-Fi as long as they are on the same network.

### 13.12 CORS Configuration (Browser-Based Scanners Only)

If the scanner app runs in a **web browser** (not a native desktop app), you need to configure CORS to allow the scanner's origin.

Open **Command Prompt as Administrator** on the server:

```cmd
setx /M Cors__AllowedOrigins__0 "http://192.168.1.50:3000"
```

For multiple scanner origins:

```cmd
setx /M Cors__AllowedOrigins__0 "http://192.168.1.50:3000"
setx /M Cors__AllowedOrigins__1 "http://192.168.1.51:3000"
```

Then restart the SmartLog service:

```cmd
sc stop SmartLogWeb & timeout /t 3 & sc start SmartLogWeb
```

> **Note:** Native desktop scanner apps (e.g., Electron, WPF) do not require CORS configuration. CORS only applies to web browsers.

### 13.13 How Scanning Works (Technical Flow)

1. A student taps or shows their QR-coded ID card to the scanner.
2. The scanner reads the QR code and sends a `POST http://192.168.1.10:5050/api/v1/scans` request with:
   - The QR payload
   - Scan time
   - Scan type (`ENTRY` or `EXIT`)
   - API key in the `X-API-Key` header
3. SmartLog validates the request:
   - Authenticates the device via API key (SHA-256 hash lookup).
   - Parses the QR code: `SMARTLOG:{studentId}:{timestamp}:{hmac}`.
   - Verifies the HMAC-SHA256 signature (constant-time comparison).
   - Checks the student is active and enrolled.
   - Checks it's a school day (not a holiday/suspension).
   - Checks for duplicate scans (5-minute window).
4. SmartLog responds with the student's name, grade, section, and acceptance status.
5. If accepted, SmartLog queues an SMS notification to the parent/guardian.

---

## 14. Network Configuration (Additional)

### 14.1 Changing the Port

If port 5050 is already in use or you want a different port, change it:

```cmd
setx /M ASPNETCORE_URLS "http://+:8080"
```

Then restart the service and add a new firewall rule:

```cmd
sc stop SmartLogWeb & timeout /t 3 & sc start SmartLogWeb
netsh advfirewall firewall add rule name="SmartLog Web" dir=in action=allow protocol=TCP localport=8080
```

> **Important:** After changing the port, update the **Server URL** on all scanner devices to use the new port (e.g., `http://192.168.1.10:8080`).

### 14.2 Access from Different Locations

| From Where | URL to Use |
|------------|------------|
| The server itself | `http://localhost:5050` |
| Another PC on the same LAN/Wi-Fi | `http://192.168.1.10:5050` |
| A scanner device on the same LAN/Wi-Fi | `http://192.168.1.10:5050` |
| A different network/subnet | Not supported without additional router configuration (port forwarding or VPN) |

### 14.3 Using a Wi-Fi Router (Typical School Setup)

Most schools use a Wi-Fi router that provides both wired and wireless connections. A typical setup:

```
┌─────────────────────────────────────────────────┐
│              School Wi-Fi Router                 │
│           (e.g., 192.168.1.1)                    │
│                                                  │
│   LAN Ports (Ethernet)     Wi-Fi (Wireless)      │
│   ┌──────┐ ┌──────┐       ┌──────┐ ┌──────┐     │
│   │Port 1│ │Port 2│       │ Wi-Fi│ │ Wi-Fi│     │
│   └──┬───┘ └──┬───┘       └──┬───┘ └──┬───┘     │
└──────┼────────┼──────────────┼────────┼──────────┘
       │        │              │        │
       ▼        ▼              ▼        ▼
   SmartLog   Scanner      Scanner   Admin
   Server     Device #1    Device #2  Laptop
   .1.10      .1.50        .1.51     .1.100
   (static)   (DHCP/static)(DHCP)    (DHCP)
```

**Key points:**
- The server should be connected via **Ethernet cable** to a LAN port on the router for stability.
- Scanner devices can connect via Ethernet or Wi-Fi.
- All devices on the router are on the same network and can communicate with each other.
- The server must have a **static IP** (Section 13.3). Scanner devices can use DHCP (automatic).

### 14.4 Verifying All Devices Are on the Same Network

On each device, run `ipconfig` and check that:
1. The **IPv4 Address** starts with the same prefix (e.g., `192.168.1.xxx`).
2. The **Subnet Mask** is the same (e.g., `255.255.255.0`).
3. The **Default Gateway** is the same (e.g., `192.168.1.1`).

If any of these differ, the devices are on different networks and cannot communicate directly.

### 14.5 Common Network Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| Scanner can't ping the server | Different network/subnet | Connect both to the same router. Verify with `ipconfig` on both devices. |
| Scanner can ping but can't reach port 5050 | Windows Firewall blocking | Re-run the firewall command from [Step 8](#9-step-8-open-the-firewall-port). |
| Connection works then drops | Wi-Fi signal weak | Move scanner closer to the router, or connect via Ethernet cable. |
| IP address changed after reboot | Server using DHCP instead of static IP | Set a static IP (Section 13.3). |
| "Connection refused" error | SmartLog service not running | Run `sc query SmartLogWeb` and `sc start SmartLogWeb` if stopped. |
| Multiple devices on same IP | IP address conflict | Ensure each device has a unique IP. Use DHCP for scanners, static only for the server. |

---

## 15. Managing the Service

### 15.1 Check Status

```cmd
sc query SmartLogWeb
```

### 15.2 Start the Service

```cmd
sc start SmartLogWeb
```

### 15.3 Stop the Service

```cmd
sc stop SmartLogWeb
```

### 15.4 Restart the Service

```cmd
sc stop SmartLogWeb & timeout /t 3 & sc start SmartLogWeb
```

### 15.5 View Logs

SmartLog writes logs to the **Windows Event Viewer**.

1. Press `Win + R`, type `eventvwr.msc`, press Enter.
2. Navigate to **Windows Logs > Application**.
3. Look for entries with Source: `.NET Runtime` or `SmartLog.Web`.

Or use the command line to view the last 20 application log entries:

```cmd
wevtutil qe Application /c:20 /f:text /rd:true
```

### 15.6 Service Behavior on Boot

The service is configured with `start=auto`, which means it starts automatically when Windows boots — no user login required.

### 15.7 Service Behavior on Crash

The service is configured with automatic recovery:
- **1st failure:** Restart after 5 seconds
- **2nd failure:** Restart after 10 seconds
- **Subsequent failures:** Restart after 30 seconds
- **Failure counter resets:** Every 24 hours

---

## 16. Backup & Restore

### 16.1 Database Backup (Command Line)

```cmd
sqlcmd -S .\SQLEXPRESS -E -Q "BACKUP DATABASE SmartLog TO DISK = 'C:\Backups\SmartLog.bak' WITH COMPRESSION"
```

> Create the `C:\Backups` folder first: `mkdir C:\Backups`

### 16.2 Database Backup (SSMS)

1. Open SQL Server Management Studio.
2. Connect to `.\SQLEXPRESS` with Windows Authentication.
3. Right-click the **SmartLog** database > **Tasks > Back Up...**
4. Set **Backup type** to **Full**.
5. Under **Destination**, set the file path (e.g., `C:\Backups\SmartLog.bak`).
6. Click **OK**.

### 16.3 Automated Daily Backup

Create a batch file `C:\Scripts\backup-smartlog.bat`:

```bat
@echo off
set BACKUP_DIR=C:\Backups
set TIMESTAMP=%date:~-4%%date:~4,2%%date:~7,2%_%time:~0,2%%time:~3,2%
set TIMESTAMP=%TIMESTAMP: =0%
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"
sqlcmd -S .\SQLEXPRESS -E -Q "BACKUP DATABASE SmartLog TO DISK = '%BACKUP_DIR%\SmartLog_%TIMESTAMP%.bak' WITH COMPRESSION, INIT"
echo Backup saved: %BACKUP_DIR%\SmartLog_%TIMESTAMP%.bak
```

Schedule it with **Windows Task Scheduler**:

1. Press `Win + R`, type `taskschd.msc`, press Enter.
2. Click **Create Basic Task** (right panel).
3. **Name:** `SmartLog Daily Backup`.
4. **Trigger:** Daily, at your preferred time (e.g., 11:00 PM).
5. **Action:** Start a program.
6. **Program/script:** `C:\Scripts\backup-smartlog.bat`.
7. Click **Finish**.

> **Tip:** Periodically copy backup files to an external drive or USB stick for off-site safety.

### 16.4 Database Restore

```cmd
REM 1. Stop the SmartLog service (releases database connections)
sc stop SmartLogWeb

REM 2. Wait for the service to stop
timeout /t 5

REM 3. Restore the database
sqlcmd -S .\SQLEXPRESS -E -Q "RESTORE DATABASE SmartLog FROM DISK = 'C:\Backups\SmartLog.bak' WITH REPLACE"

REM 4. Start the service
sc start SmartLogWeb
```

### 16.5 Backup Uploaded Files

Profile pictures and uploaded files are stored in `C:\SmartLog\wwwroot\uploads\`. Copy this folder to your backup location:

```cmd
xcopy C:\SmartLog\wwwroot\uploads C:\Backups\uploads /E /I /Y
```

### 16.6 Full Backup Checklist

| What to Back Up | Location | How Often |
|-----------------|----------|-----------|
| Database | `C:\Backups\SmartLog_*.bak` | Daily (automated) |
| Uploaded files | `C:\SmartLog\wwwroot\uploads\` | Weekly (manual) |
| Environment variables | Documented in your records | After any change |
| HMAC secret key | Documented securely | Once (keep safe) |

---

## 17. Updating SmartLog

When a new version of SmartLog is available:

### 17.1 Pull the Latest Code

```cmd
cd C:\Source\SmartLogWebApp
git pull
```

### 17.2 Stop the Service

```cmd
sc stop SmartLogWeb
```

### 17.3 Wait for the Service to Stop

```cmd
timeout /t 5
```

### 17.4 Rebuild and Publish

```cmd
dotnet publish src\SmartLog.Web -p:PublishProfile=WinX64 -o C:\SmartLog
```

### 17.5 Start the Service

```cmd
sc start SmartLogWeb
```

Database migrations are applied automatically on startup — no manual migration step is needed.

### 17.6 Verify the Update

```cmd
sc query SmartLogWeb
curl http://localhost:5050/health
```

### All-in-One Update Command

```cmd
cd C:\Source\SmartLogWebApp && git pull && sc stop SmartLogWeb && timeout /t 5 && dotnet publish src\SmartLog.Web -p:PublishProfile=WinX64 -o C:\SmartLog && sc start SmartLogWeb
```

---

## 18. Troubleshooting

### Service Won't Start

**Step 1: Check the Event Viewer for errors.**

```cmd
wevtutil qe Application /c:10 /f:text /rd:true
```

**Step 2: Check common causes:**

| Symptom | Cause | Fix |
|---------|-------|-----|
| Service starts then stops immediately | Missing environment variables | Open a new cmd and run `echo %SMARTLOG_DB_CONNECTION%`. If blank, re-run the `setx /M` commands from Step 5. |
| "Login failed" in logs | SQL Server not running | Run `net start MSSQL$SQLEXPRESS`. |
| "Cannot open database SmartLog" | Database doesn't exist | Run `sqlcmd -S .\SQLEXPRESS -E -Q "CREATE DATABASE SmartLog"`. |
| "An error occurred while migrating" | Database permissions issue | Ensure the LocalSystem account has access (it should by default with Windows Auth). |
| "Address already in use" | Port 5050 occupied by another app | Find it with `netstat -ano | findstr :5050`. Change port or stop the other app. |
| "Access denied" on COM port | GSM modem port locked by another program | Close other serial port software. Check correct COM port in Device Manager. |

### Test Database Connection

```cmd
sqlcmd -S .\SQLEXPRESS -E -Q "SELECT COUNT(*) AS TableCount FROM SmartLog.INFORMATION_SCHEMA.TABLES"
```

If this returns a number, the database is accessible and tables have been created.

### Test Application Manually (Without Service)

Stop the service and run the app directly to see console output:

```cmd
sc stop SmartLogWeb
C:\SmartLog\SmartLog.Web.exe
```

This runs the app in the foreground and shows all log output in the console. Press `Ctrl+C` to stop, then restart the service:

```cmd
sc start SmartLogWeb
```

### Check What's Using a Port

```cmd
netstat -ano | findstr :5050
```

The last column is the PID. Find the process:

```cmd
tasklist /FI "PID eq 12345"
```

### Reset Admin Password (Without Losing Data)

Connect to the database and remove the admin user. SmartLog will re-create it on next restart with the current seed password:

```cmd
sqlcmd -S .\SQLEXPRESS -E -Q "DELETE FROM SmartLog.dbo.AspNetUserRoles WHERE UserId IN (SELECT Id FROM SmartLog.dbo.AspNetUsers WHERE UserName = 'super.admin'); DELETE FROM SmartLog.dbo.AspNetUsers WHERE UserName = 'super.admin'"

sc stop SmartLogWeb & timeout /t 3 & sc start SmartLogWeb
```

### Reset Everything (Fresh Start)

```cmd
REM WARNING: This deletes ALL data and starts over!
sc stop SmartLogWeb
sqlcmd -S .\SQLEXPRESS -E -Q "DROP DATABASE SmartLog"
sqlcmd -S .\SQLEXPRESS -E -Q "CREATE DATABASE SmartLog"
sc start SmartLogWeb
```

---

## 19. Uninstalling

### 19.1 Stop and Remove the Service

```cmd
sc stop SmartLogWeb
sc delete SmartLogWeb
```

### 19.2 Remove Application Files

```cmd
rmdir /s /q C:\SmartLog
```

### 19.3 Remove Source Code

```cmd
rmdir /s /q C:\Source\SmartLogWebApp
```

### 19.4 Remove Environment Variables

Open **Command Prompt as Administrator**:

```cmd
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v SMARTLOG_DB_CONNECTION /f
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v SMARTLOG_HMAC_SECRET_KEY /f
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v SMARTLOG_SEED_PASSWORD /f
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v ASPNETCORE_URLS /f
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v ASPNETCORE_ENVIRONMENT /f
```

### 19.5 Remove Firewall Rule

```cmd
netsh advfirewall firewall delete rule name="SmartLog Web"
```

### 19.6 Remove Backup Files

```cmd
rmdir /s /q C:\Backups
rmdir /s /q C:\Scripts
```

### 19.7 Drop the Database (Destroys All Data)

```cmd
sqlcmd -S .\SQLEXPRESS -E -Q "DROP DATABASE SmartLog"
```

### 19.8 Uninstall SQL Server (Optional)

1. Open **Settings > Apps > Installed Apps**.
2. Find **SQL Server 2022** and click **Uninstall**.

---

## 20. Quick Reference

### Key Paths

| Item | Path |
|------|------|
| Published application | `C:\SmartLog\` |
| Application executable | `C:\SmartLog\SmartLog.Web.exe` |
| Configuration file | `C:\SmartLog\appsettings.json` |
| Uploaded files | `C:\SmartLog\wwwroot\uploads\` |
| Source code | `C:\Source\SmartLogWebApp\` |
| Backup files | `C:\Backups\` |

### URLs

| Item | URL |
|------|-----|
| Application | `http://localhost:5050` |
| Health check | `http://localhost:5050/health` |
| LAN access | `http://<server-ip>:5050` |
| Scanner API | `POST http://<server-ip>:5050/api/v1/scans` |
| Scanner health | `GET http://<server-ip>:5050/api/v1/health` |

### Service Commands

| Action | Command |
|--------|---------|
| Check status | `sc query SmartLogWeb` |
| Start | `sc start SmartLogWeb` |
| Stop | `sc stop SmartLogWeb` |
| Restart | `sc stop SmartLogWeb & timeout /t 3 & sc start SmartLogWeb` |
| View logs | `eventvwr.msc` > Windows Logs > Application |
| Run manually | `sc stop SmartLogWeb` then `C:\SmartLog\SmartLog.Web.exe` |

### Default Accounts

| Username | Password | Role |
|----------|----------|------|
| `super.admin` | `<SMARTLOG_SEED_PASSWORD>` | SuperAdmin |
| `admin.amy` | `<SMARTLOG_SEED_PASSWORD>` | Admin |
| `teacher.tina` | `<SMARTLOG_SEED_PASSWORD>` | Teacher |
| `guard.gary` | `<SMARTLOG_SEED_PASSWORD>` | Security |
| `staff.sarah` | `<SMARTLOG_SEED_PASSWORD>` | Staff |

### Environment Variables

| Variable | Required | Example |
|----------|----------|---------|
| `SMARTLOG_DB_CONNECTION` | Yes | `Server=.\SQLEXPRESS;Database=SmartLog;Trusted_Connection=true;TrustServerCertificate=true` |
| `SMARTLOG_HMAC_SECRET_KEY` | Yes | Random 32+ character Base64 string |
| `SMARTLOG_SEED_PASSWORD` | Yes | `SmartLog@2026!` |
| `ASPNETCORE_URLS` | Yes | `http://+:5050` |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` |
| `Cors__AllowedOrigins__0` | No | `http://192.168.1.50:3000` |

### SQL Server Commands

| Action | Command |
|--------|---------|
| Check SQL Server is running | `sc query MSSQL$SQLEXPRESS` |
| Start SQL Server | `net start MSSQL$SQLEXPRESS` |
| Create database | `sqlcmd -S .\SQLEXPRESS -E -Q "CREATE DATABASE SmartLog"` |
| Backup database | `sqlcmd -S .\SQLEXPRESS -E -Q "BACKUP DATABASE SmartLog TO DISK = 'C:\Backups\SmartLog.bak' WITH COMPRESSION"` |
| Restore database | `sqlcmd -S .\SQLEXPRESS -E -Q "RESTORE DATABASE SmartLog FROM DISK = 'C:\Backups\SmartLog.bak' WITH REPLACE"` |
| Drop database | `sqlcmd -S .\SQLEXPRESS -E -Q "DROP DATABASE SmartLog"` |
