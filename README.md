# SmartLog Web App

An offline-first, LAN-based School Information Management System for Philippine K-12 schools. Tracks student attendance via QR code scanning, sends SMS notifications to parents, and provides administrative tools for managing students, faculty, academic years, and reports.

**Stack:** ASP.NET Core 8.0 Razor Pages · EF Core · SQL Server · Serilog  
**Deployment:** Windows Service or Docker · Designed for school LAN (no internet required)

---

## Documentation

| Doc | Description |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System architecture — how WebApp and ScannerApp communicate |
| [docs/FEATURES.md](docs/FEATURES.md) | Full feature list by module |
| [docs/TECHNICAL.md](docs/TECHNICAL.md) | Architecture, data model, auth, process flows, dev commands |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Docker and Windows Service setup, network configuration, SMS setup |
| [docs/API.md](docs/API.md) | REST API reference for scanner devices and the dashboard |

---

## Quick Start (Docker)

```bash
git clone <repository-url>
cd SmartLogWebApp
docker-compose up --build -d
```

Open **http://localhost:8080**. Default login: `super.admin` / `SecurePass1!`

For production deployment on a Windows machine, see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

---

## Project Structure

```
SmartLogWebApp/
├── src/SmartLog.Web/     # ASP.NET Core application
├── tests/                # xUnit test suite (~302 tests)
├── deploy/               # Windows deployment scripts
├── docs/                 # Technical documentation
├── sdlc-studio/          # SDLC artefacts (epics, stories, bugs)
└── docker-compose.yml
```

---

## Related

- **SmartLogScannerApp** — The gate scanner client (separate repo). Reads student QR codes and submits scans to this app via `POST /api/v1/scans`.

---

## License

Proprietary — All rights reserved.
