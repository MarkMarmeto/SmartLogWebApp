#!/bin/bash

# Database Reset Script for Profile Picture Implementation
# This script will drop and recreate the database with ProfilePicturePath columns

echo "=========================================="
echo "SmartLog Database Reset Script"
echo "=========================================="
echo ""

export PATH="/usr/local/share/dotnet:$PATH:/Users/markmarmeto/.dotnet/tools"
PROJECT_DIR="/Users/markmarmeto/Projects/SmartLogWebApp/src/SmartLog.Web"

cd "$PROJECT_DIR" || exit 1

echo "Step 1: Dropping existing database..."
dotnet-ef database drop --force 2>&1
echo ""

echo "Step 2: Applying all migrations (creating fresh database)..."
dotnet-ef database update 2>&1
echo ""

echo "=========================================="
echo "Database reset complete!"
echo "=========================================="
echo ""
echo "The database now includes ProfilePicturePath columns in:"
echo "  - AspNetUsers (ApplicationUser)"
echo "  - Students"
echo "  - Faculties"
echo ""
echo "Next steps:"
echo "1. Run the application: dotnet run"
echo "2. Login and test profile picture upload"
echo ""
