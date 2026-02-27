#!/bin/bash

echo "=========================================="
echo "SmartLog Complete Database Reset"
echo "=========================================="
echo ""

export PATH="/usr/local/share/dotnet:$PATH:/Users/markmarmeto/.dotnet/tools"
PROJECT_DIR="/Users/markmarmeto/Projects/SmartLogWebApp/src/SmartLog.Web"

cd "$PROJECT_DIR" || exit 1

# Step 1: Force drop database
echo "Step 1: Force dropping database..."
dotnet-ef database drop --force --no-build 2>&1 | grep -v "FTL\|HostAbortedException"
echo "Database dropped"
echo ""

# Step 2: Remove all migration files (clean slate)
echo "Step 2: Cleaning migration files..."
rm -rf Data/Migrations/*.cs 2>/dev/null || true
echo "Migration files cleaned"
echo ""

# Step 3: Create fresh migration
echo "Step 3: Creating fresh migration with ProfilePicturePath..."
dotnet-ef migrations add InitialWithProfilePicture 2>&1 | grep -v "FTL\|HostAbortedException"
echo "Migration created"
echo ""

# Step 4: Apply migration (create database)
echo "Step 4: Creating database with all tables..."
dotnet-ef database update 2>&1 | grep -v "FTL\|HostAbortedException"
echo "Database created"
echo ""

# Step 5: Run the application to seed data
echo "Step 5: Starting application to seed data..."
echo "The app will seed default users and data on first run."
echo ""
echo "=========================================="
echo "Database Reset Complete!"
echo "=========================================="
echo ""
echo "Default users created:"
echo "  - Username: super.admin   Password: SecurePass1!  Role: SuperAdmin"
echo "  - Username: admin.amy     Password: SecurePass1!  Role: Admin"
echo "  - Username: teacher.tina  Password: SecurePass1!  Role: Teacher"
echo "  - Username: guard.gary    Password: SecurePass1!  Role: Security"
echo "  - Username: staff.sarah   Password: SecurePass1!  Role: Staff"
echo ""
echo "Database includes:"
echo "  - ProfilePicturePath columns in AspNetUsers, Students, and Faculties"
echo "  - Grade levels (K-12)"
echo "  - Sections (A, B, C for each grade)"
echo "  - Academic years"
echo "  - Test faculty members"
echo ""
echo "To start testing:"
echo "  cd $PROJECT_DIR"
echo "  dotnet run"
echo ""
echo "Then login with one of the accounts above."
echo ""
