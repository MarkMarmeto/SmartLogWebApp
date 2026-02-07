# SmartLog - School Information Management System

An offline-first, LAN-only School Information Management System built with ASP.NET Core 8.0 Razor Pages.

## Features

- **User Authentication** - Secure login with ASP.NET Identity
- **Role-Based Access Control** - 5 roles: SuperAdmin, Admin, Teacher, Security, Staff
- **Student Management** - CRUD operations with QR code generation (coming soon)
- **Faculty Management** - Staff records management (coming soon)
- **Attendance Tracking** - QR-based attendance system (coming soon)
- **SMS Notifications** - Parent notifications via GSM modem (coming soon)

## Prerequisites

- **Docker Desktop** (recommended) or
- **.NET 8.0 SDK** (for local development)

## Quick Start with Docker

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd SmartLogWebApp
   ```

2. **Start the application**
   ```bash
   docker compose up -d
   ```

3. **Access the application**
   - Open http://localhost:8080 in your browser

4. **Default credentials**
   | Username | Password | Role |
   |----------|----------|------|
   | admin.amy | SecurePass1! | Admin |
   | super.admin | SecurePass1! | SuperAdmin |

## Local Development (without Docker)

1. **Install .NET 8.0 SDK**
   - Download from https://dotnet.microsoft.com/download/dotnet/8.0

2. **Start SQL Server** (using Docker)
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=SmartLog2026!" \
     -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
   ```

3. **Run the application**
   ```bash
   cd src/SmartLog.Web
   dotnet run
   ```

4. **Access the application**
   - Open https://localhost:7001 in your browser

## Project Structure

```
SmartLogWebApp/
├── src/
│   └── SmartLog.Web/           # Main web application
│       ├── Data/               # Database context and entities
│       │   ├── Entities/       # Domain models
│       │   └── Migrations/     # EF Core migrations
│       ├── Pages/              # Razor Pages
│       │   ├── Account/        # Authentication pages
│       │   └── Shared/         # Layouts and partials
│       └── Services/           # Business logic (coming soon)
├── sdlc-studio/                # Project documentation
│   ├── prd.md                  # Product Requirements
│   ├── trd.md                  # Technical Requirements
│   ├── epics/                  # Feature epics
│   └── stories/                # User stories
└── docker-compose.yml          # Container orchestration
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | See appsettings.json |
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | Production |

### Docker Compose Services

| Service | Port | Description |
|---------|------|-------------|
| smartlog-web | 8080 | Web application |
| smartlog-db | 1433 | SQL Server Express |

## Development Notes

### Database Migrations

Migrations are applied automatically on startup. To add a new migration manually:

```bash
cd src/SmartLog.Web
dotnet ef migrations add <MigrationName>
```

### Seed Data

The application seeds the following on first run:
- 5 roles: SuperAdmin, Admin, Teacher, Security, Staff
- Default admin user (admin.amy)
- Default super admin user (super.admin)
- Inactive test user (inactive.user)

## Security

- Passwords are hashed using bcrypt (work factor 12)
- Sessions expire after 10 hours with sliding expiration
- Cookies are HttpOnly, Secure, SameSite=Strict
- Account lockout after 5 failed attempts (15 minutes)
- CSRF protection via anti-forgery tokens

## License

Proprietary - All rights reserved.

---

Built with SDLC Studio | SmartLog v1.0.0
