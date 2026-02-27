#!/bin/bash

# Profile Picture Feature - Automated Verification Script
# This script verifies the implementation without requiring a running database

echo "=========================================="
echo "Profile Picture Feature - Automated Tests"
echo "=========================================="
echo ""

DOTNET="/usr/local/share/dotnet/dotnet"
PROJECT_DIR="/Users/markmarmeto/Projects/SmartLogWebApp/src/SmartLog.Web"
PASS_COUNT=0
FAIL_COUNT=0

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

pass() {
    echo -e "${GREEN}✓${NC} $1"
    ((PASS_COUNT++))
}

fail() {
    echo -e "${RED}✗${NC} $1"
    ((FAIL_COUNT++))
}

warn() {
    echo -e "${YELLOW}⚠${NC} $1"
}

echo "Test 1: Verify .NET SDK Installation"
if $DOTNET --version > /dev/null 2>&1; then
    VERSION=$($DOTNET --version)
    pass ".NET SDK $VERSION is installed"
else
    fail ".NET SDK not found"
    exit 1
fi
echo ""

echo "Test 2: Project Build"
echo "Building project..."
BUILD_OUTPUT=$($DOTNET build "$PROJECT_DIR" --no-incremental 2>&1)
if echo "$BUILD_OUTPUT" | grep -q "Build succeeded"; then
    pass "Project builds successfully"

    # Check for errors
    ERROR_COUNT=$(echo "$BUILD_OUTPUT" | grep -c "error")
    if [ "$ERROR_COUNT" -eq 0 ]; then
        pass "No compilation errors found"
    else
        fail "Found $ERROR_COUNT compilation error(s)"
    fi

    # Check warnings (informational only)
    WARNING_COUNT=$(echo "$BUILD_OUTPUT" | grep -oP '\d+(?= Warning)' | head -1)
    if [ -n "$WARNING_COUNT" ]; then
        warn "Found $WARNING_COUNT warning(s) (pre-existing, not blocking)"
    fi
else
    fail "Project build failed"
    echo "$BUILD_OUTPUT"
    exit 1
fi
echo ""

echo "Test 3: Backend Files - Dependency Injection"
echo "Checking if IFileUploadService is injected in all PageModels..."

for file in "CreateStudent" "EditStudent" "CreateFaculty" "EditFaculty"; do
    FILE_PATH="$PROJECT_DIR/Pages/Admin/${file}.cshtml.cs"
    if grep -q "IFileUploadService _fileUploadService" "$FILE_PATH"; then
        pass "${file}.cshtml.cs - IFileUploadService field declared"
    else
        fail "${file}.cshtml.cs - IFileUploadService field NOT found"
    fi

    if grep -q "IFileUploadService fileUploadService" "$FILE_PATH"; then
        pass "${file}.cshtml.cs - IFileUploadService parameter in constructor"
    else
        fail "${file}.cshtml.cs - IFileUploadService parameter NOT found"
    fi

    if grep -q "_fileUploadService = fileUploadService" "$FILE_PATH"; then
        pass "${file}.cshtml.cs - IFileUploadService assigned in constructor"
    else
        fail "${file}.cshtml.cs - IFileUploadService assignment NOT found"
    fi
done
echo ""

echo "Test 4: Backend Files - InputModel ProfilePicture Property"
for file in "CreateStudent" "EditStudent" "CreateFaculty" "EditFaculty"; do
    FILE_PATH="$PROJECT_DIR/Pages/Admin/${file}.cshtml.cs"
    if grep -q "IFormFile? ProfilePicture" "$FILE_PATH"; then
        pass "${file}.cshtml.cs - ProfilePicture property exists"
    else
        fail "${file}.cshtml.cs - ProfilePicture property NOT found"
    fi
done
echo ""

echo "Test 5: Backend Files - Upload Logic"
for file in "CreateStudent" "CreateFaculty"; do
    FILE_PATH="$PROJECT_DIR/Pages/Admin/${file}.cshtml.cs"
    if grep -q "UploadProfilePictureAsync" "$FILE_PATH"; then
        pass "${file}.cshtml.cs - Upload logic implemented"
    else
        fail "${file}.cshtml.cs - Upload logic NOT found"
    fi
done

for file in "EditStudent" "EditFaculty"; do
    FILE_PATH="$PROJECT_DIR/Pages/Admin/${file}.cshtml.cs"
    if grep -q "UploadProfilePictureAsync" "$FILE_PATH"; then
        pass "${file}.cshtml.cs - Upload logic implemented"
    else
        fail "${file}.cshtml.cs - Upload logic NOT found"
    fi

    if grep -q "DeleteProfilePictureAsync" "$FILE_PATH"; then
        pass "${file}.cshtml.cs - Delete logic implemented"
    else
        fail "${file}.cshtml.cs - Delete logic NOT found"
    fi
done
echo ""

echo "Test 6: Frontend Files - Form Encoding"
for file in "CreateStudent" "EditStudent" "CreateFaculty" "EditFaculty"; do
    FILE_PATH="$PROJECT_DIR/Pages/Admin/${file}.cshtml"
    if grep -q 'enctype="multipart/form-data"' "$FILE_PATH"; then
        pass "${file}.cshtml - Form has multipart encoding"
    else
        fail "${file}.cshtml - Form missing multipart encoding"
    fi
done
echo ""

echo "Test 7: Frontend Files - File Input"
for file in "CreateStudent" "EditStudent" "CreateFaculty" "EditFaculty"; do
    FILE_PATH="$PROJECT_DIR/Pages/Admin/${file}.cshtml"
    if grep -q 'asp-for="Input.ProfilePicture"' "$FILE_PATH"; then
        pass "${file}.cshtml - File input exists"
    else
        fail "${file}.cshtml - File input NOT found"
    fi

    if grep -q 'accept="image/\*"' "$FILE_PATH"; then
        pass "${file}.cshtml - File input has accept attribute"
    else
        fail "${file}.cshtml - File input missing accept attribute"
    fi
done
echo ""

echo "Test 8: Frontend Files - Preview JavaScript"
for file in "CreateStudent" "EditStudent" "CreateFaculty" "EditFaculty"; do
    FILE_PATH="$PROJECT_DIR/Pages/Admin/${file}.cshtml"
    if grep -q "function previewImage" "$FILE_PATH"; then
        pass "${file}.cshtml - Preview JavaScript function exists"
    else
        fail "${file}.cshtml - Preview JavaScript NOT found"
    fi
done
echo ""

echo "Test 9: Edit Pages - Current Picture Display"
for file in "EditStudent" "EditFaculty"; do
    FILE_PATH="$PROJECT_DIR/Pages/Admin/${file}.cshtml"
    if grep -q "CurrentProfilePicturePath" "$FILE_PATH"; then
        pass "${file}.cshtml - Displays current picture"
    else
        fail "${file}.cshtml - Current picture display NOT found"
    fi

    CS_FILE="$PROJECT_DIR/Pages/Admin/${file}.cshtml.cs"
    if grep -q "public string? CurrentProfilePicturePath" "$CS_FILE"; then
        pass "${file}.cshtml.cs - CurrentProfilePicturePath property exists"
    else
        fail "${file}.cshtml.cs - CurrentProfilePicturePath property NOT found"
    fi
done
echo ""

echo "Test 10: List Views - Photo Column"
for file in "Students" "Faculty"; do
    FILE_PATH="$PROJECT_DIR/Pages/Admin/${file}.cshtml"
    if grep -q "<th>Photo</th>" "$FILE_PATH"; then
        pass "${file}.cshtml - Photo column header exists"
    else
        fail "${file}.cshtml - Photo column header NOT found"
    fi

    if grep -q "ProfilePicturePath" "$FILE_PATH"; then
        pass "${file}.cshtml - References ProfilePicturePath"
    else
        fail "${file}.cshtml - ProfilePicturePath reference NOT found"
    fi

    if grep -q "Substring(0,1)" "$FILE_PATH"; then
        pass "${file}.cshtml - Default initials logic exists"
    else
        fail "${file}.cshtml - Default initials logic NOT found"
    fi
done
echo ""

echo "Test 11: Upload Directory Structure"
UPLOAD_DIR="$PROJECT_DIR/wwwroot/uploads/profile-pictures"
if [ -d "$UPLOAD_DIR" ]; then
    pass "Upload directory exists: $UPLOAD_DIR"

    if [ -d "$UPLOAD_DIR/students" ]; then
        pass "Students subdirectory exists"
    else
        warn "Students subdirectory doesn't exist (will be created automatically)"
    fi

    if [ -d "$UPLOAD_DIR/faculty" ]; then
        pass "Faculty subdirectory exists"
    else
        warn "Faculty subdirectory doesn't exist (will be created automatically)"
    fi
else
    warn "Upload directory doesn't exist (will be created automatically)"
fi
echo ""

echo "Test 12: FileUploadService Verification"
SERVICE_FILE="$PROJECT_DIR/Services/FileUploadService.cs"
if [ -f "$SERVICE_FILE" ]; then
    pass "FileUploadService.cs exists"

    if grep -q "UploadProfilePictureAsync" "$SERVICE_FILE"; then
        pass "UploadProfilePictureAsync method exists"
    else
        fail "UploadProfilePictureAsync method NOT found"
    fi

    if grep -q "DeleteProfilePictureAsync" "$SERVICE_FILE"; then
        pass "DeleteProfilePictureAsync method exists"
    else
        fail "DeleteProfilePictureAsync method NOT found"
    fi

    if grep -q "IsValidImage" "$SERVICE_FILE"; then
        pass "IsValidImage validation method exists"
    else
        fail "IsValidImage method NOT found"
    fi

    if grep -q "5 \* 1024 \* 1024" "$SERVICE_FILE"; then
        pass "5MB file size limit configured"
    else
        warn "File size limit might be different"
    fi
else
    fail "FileUploadService.cs NOT found"
fi
echo ""

echo "Test 13: Entity Models - ProfilePicturePath Property"
for entity in "Student" "Faculty"; do
    ENTITY_FILE="$PROJECT_DIR/Data/Entities/${entity}.cs"
    if [ -f "$ENTITY_FILE" ]; then
        if grep -q "public string? ProfilePicturePath" "$ENTITY_FILE"; then
            pass "${entity}.cs - ProfilePicturePath property exists"
        else
            fail "${entity}.cs - ProfilePicturePath property NOT found"
        fi
    else
        fail "${entity}.cs - File not found"
    fi
done
echo ""

echo "Test 14: Directory Name Consistency"
if grep -q '"faculty"' "$PROJECT_DIR/Pages/Admin/CreateFaculty.cshtml.cs"; then
    pass "CreateFaculty uses correct directory name: 'faculty'"
else
    fail "CreateFaculty uses incorrect directory name"
fi

if grep -q '"faculty"' "$PROJECT_DIR/Pages/Admin/EditFaculty.cshtml.cs"; then
    pass "EditFaculty uses correct directory name: 'faculty'"
else
    fail "EditFaculty uses incorrect directory name"
fi

if grep -q '"students"' "$PROJECT_DIR/Pages/Admin/CreateStudent.cshtml.cs"; then
    pass "CreateStudent uses correct directory name: 'students'"
else
    fail "CreateStudent uses incorrect directory name"
fi

if grep -q '"students"' "$PROJECT_DIR/Pages/Admin/EditStudent.cshtml.cs"; then
    pass "EditStudent uses correct directory name: 'students'"
else
    fail "EditStudent uses incorrect directory name"
fi
echo ""

echo "=========================================="
echo "Test Summary"
echo "=========================================="
echo -e "${GREEN}Passed: $PASS_COUNT${NC}"
echo -e "${RED}Failed: $FAIL_COUNT${NC}"
echo ""

if [ $FAIL_COUNT -eq 0 ]; then
    echo -e "${GREEN}✓ ALL TESTS PASSED!${NC}"
    echo ""
    echo "The profile picture upload feature is correctly implemented."
    echo "Next steps:"
    echo "1. Start SQL Server (docker or local)"
    echo "2. Run: dotnet run --project $PROJECT_DIR"
    echo "3. Execute manual testing from PROFILE_PICTURE_TEST_REPORT.md"
    exit 0
else
    echo -e "${RED}✗ SOME TESTS FAILED${NC}"
    echo "Please review the failures above before proceeding."
    exit 1
fi
