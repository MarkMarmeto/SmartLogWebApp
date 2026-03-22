using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Data;

/// <summary>
/// Database initializer for seeding roles and default users.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Seed roles, default users, and test faculty records.
    /// This method is idempotent - safe to run multiple times.
    /// </summary>
    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context,
        ILogger logger)
    {
        // Define the 5 roles per TRD
        string[] roles = { "SuperAdmin", "Admin", "Teacher", "Security", "Staff" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role: {Role}", role);
            }
        }

        // Seed default admin user (Admin Amy from personas)
        const string adminUserName = "admin.amy";
        const string adminEmail = "admin.amy@smartlog.local";
        var adminPassword = Environment.GetEnvironmentVariable("SMARTLOG_SEED_PASSWORD");

        if (string.IsNullOrEmpty(adminPassword))
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (environment == "Production")
            {
                logger.LogError("SMARTLOG_SEED_PASSWORD environment variable is required in production");
                throw new InvalidOperationException("SMARTLOG_SEED_PASSWORD environment variable must be set in production.");
            }

            adminPassword = "SecurePass1!";
            logger.LogWarning("Using default seed password for development. Set SMARTLOG_SEED_PASSWORD environment variable for production.");
        }

        var existingUser = await userManager.FindByNameAsync(adminUserName);
        if (existingUser == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminUserName,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "Amy",
                LastName = "Administrator",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Created default admin user: {UserName}", adminUserName);
            }
            else
            {
                logger.LogError("Failed to create admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Seed a SuperAdmin user (Tech-Savvy Tony)
        const string superAdminUserName = "super.admin";
        const string superAdminEmail = "super.admin@smartlog.local";

        var existingSuperAdmin = await userManager.FindByNameAsync(superAdminUserName);
        if (existingSuperAdmin == null)
        {
            var superAdminUser = new ApplicationUser
            {
                UserName = superAdminUserName,
                Email = superAdminEmail,
                EmailConfirmed = true,
                FirstName = "Tony",
                LastName = "SuperAdmin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(superAdminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(superAdminUser, "SuperAdmin");
                logger.LogInformation("Created default super admin user: {UserName}", superAdminUserName);
            }
        }

        // Seed an inactive user for testing AC4
        const string inactiveUserName = "inactive.user";
        var existingInactive = await userManager.FindByNameAsync(inactiveUserName);
        if (existingInactive == null)
        {
            var inactiveUser = new ApplicationUser
            {
                UserName = inactiveUserName,
                Email = "inactive@smartlog.local",
                EmailConfirmed = true,
                FirstName = "Inactive",
                LastName = "User",
                IsActive = false, // Deactivated user
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(inactiveUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(inactiveUser, "Staff");
                logger.LogInformation("Created inactive test user: {UserName}", inactiveUserName);
            }
        }

        // Seed Teacher Tina for testing role-based features
        await SeedUserAsync(userManager, logger, new ApplicationUser
        {
            UserName = "teacher.tina",
            Email = "teacher.tina@smartlog.local",
            FirstName = "Tina",
            LastName = "Teacher",
            IsActive = true
        }, adminPassword, "Teacher");

        // Seed Guard Gary (Security) for testing role-based features
        await SeedUserAsync(userManager, logger, new ApplicationUser
        {
            UserName = "guard.gary",
            Email = "guard.gary@smartlog.local",
            FirstName = "Gary",
            LastName = "Guard",
            IsActive = true
        }, adminPassword, "Security");

        // Seed Staff Sarah for testing role-based features
        await SeedUserAsync(userManager, logger, new ApplicationUser
        {
            UserName = "staff.sarah",
            Email = "staff.sarah@smartlog.local",
            FirstName = "Sarah",
            LastName = "Staff",
            IsActive = true
        }, adminPassword, "Staff");

        // Seed test faculty members
        await SeedFacultyAsync(context, userManager, logger);

        // Seed grade levels, sections, and academic years
        await SeedGradeLevelsAsync(context, logger);
        await SeedAcademicYearsAsync(context, logger);
        await SeedSectionsAsync(context, logger);

        // Seed calendar events (holidays)
        await SeedCalendarEventsAsync(context, logger);

        // Seed SMS templates
        await SeedSmsTemplatesAsync(context, logger);

        // Seed test students and attendance data
        await SeedStudentsAndScansAsync(context, logger);

        // Migrate existing students to enrollments
        await MigrateExistingStudentsToEnrollmentsAsync(context, logger);
    }

    private static async Task SeedUserAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        ApplicationUser user,
        string password,
        string role)
    {
        var existing = await userManager.FindByNameAsync(user.UserName!);
        if (existing == null)
        {
            user.EmailConfirmed = true;
            user.CreatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
                logger.LogInformation("Created test user: {UserName} with role {Role}", user.UserName, role);
            }
            else
            {
                logger.LogError("Failed to create user {UserName}: {Errors}",
                    user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedFacultyAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        // Only seed if no faculty exist
        if (context.Faculties.Any())
        {
            return;
        }

        var teacherTina = await userManager.FindByNameAsync("teacher.tina");
        var guardGary = await userManager.FindByNameAsync("guard.gary");

        var facultyMembers = new List<Faculty>
        {
            new Faculty
            {
                EmployeeId = "EMP-2024-001",
                FirstName = "Tina",
                LastName = "Teacher",
                Department = "English",
                Position = "Senior Teacher",
                Email = "teacher.tina@smartlog.local",
                PhoneNumber = "+63 912 345 6789",
                HireDate = new DateTime(2024, 1, 15),
                UserId = teacherTina?.Id,
                IsActive = true
            },
            new Faculty
            {
                EmployeeId = "EMP-2024-002",
                FirstName = "Gary",
                LastName = "Guard",
                Department = "Administration",
                Position = "Security Guard",
                Email = "guard.gary@smartlog.local",
                PhoneNumber = "+63 912 345 6790",
                HireDate = new DateTime(2024, 2, 1),
                UserId = guardGary?.Id,
                IsActive = true
            },
            new Faculty
            {
                EmployeeId = "EMP-2023-015",
                FirstName = "Maria",
                LastName = "Santos",
                Department = "Mathematics",
                Position = "Department Head",
                Email = "maria.santos@smartlog.local",
                PhoneNumber = "+63 912 345 6791",
                HireDate = new DateTime(2023, 6, 10),
                UserId = null, // Not linked to any user account
                IsActive = true
            },
            new Faculty
            {
                EmployeeId = "EMP-2025-003",
                FirstName = "Carlos",
                LastName = "Reyes",
                Department = "Science",
                Position = "Lab Instructor",
                Email = "carlos.reyes@smartlog.local",
                PhoneNumber = "+63 912 345 6792",
                HireDate = new DateTime(2025, 1, 5),
                UserId = null,
                IsActive = true
            },
            new Faculty
            {
                EmployeeId = "EMP-2022-008",
                FirstName = "Linda",
                LastName = "Cruz",
                Department = "Filipino",
                Position = "Senior Teacher",
                Email = "linda.cruz@smartlog.local",
                HireDate = new DateTime(2022, 8, 20),
                UserId = null,
                IsActive = false // Inactive faculty member for testing
            }
        };

        context.Faculties.AddRange(facultyMembers);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} test faculty members", facultyMembers.Count);
    }

    private static async Task SeedStudentsAndScansAsync(
        ApplicationDbContext context,
        ILogger logger)
    {
        // Only seed if no students exist
        if (context.Students.Any())
        {
            return;
        }

        // Register a test device first (skip if already exists)
        var testDevice = await context.Devices.FirstOrDefaultAsync(d => d.Name == "Main Gate Scanner (Test)");
        if (testDevice == null)
        {
            testDevice = new Device
            {
                Id = Guid.NewGuid(),
                Name = "Main Gate Scanner (Test)",
                Location = "Main Gate",
                Description = "Test scanner device for seeding data",
                ApiKeyHash = "test-hash", // Not used for test device
                IsActive = true,
                RegisteredAt = DateTime.UtcNow,
                RegisteredBy = context.Users.First(u => u.UserName == "super.admin").Id
            };

            context.Devices.Add(testDevice);
            await context.SaveChangesAsync();

            logger.LogInformation("Seeded test device: {DeviceName}", testDevice.Name);
        }
        else
        {
            logger.LogInformation("Test device already exists, skipping");
        }

        // Now seed students (this will be done in a future user story, but for testing attendance we need some data)
        logger.LogInformation("Note: Student seeding will be implemented when US0015 is completed");
    }

    private static async Task SeedGradeLevelsAsync(ApplicationDbContext context, ILogger logger)
    {
        // Only seed if no grade levels exist
        if (context.GradeLevels.Any())
        {
            logger.LogInformation("Grade levels already seeded, skipping");
            return;
        }

        var gradeLevels = new List<GradeLevel>
        {
            new GradeLevel { Code = "7", Name = "Grade 7", SortOrder = 1, IsActive = true },
            new GradeLevel { Code = "8", Name = "Grade 8", SortOrder = 2, IsActive = true },
            new GradeLevel { Code = "9", Name = "Grade 9", SortOrder = 3, IsActive = true },
            new GradeLevel { Code = "10", Name = "Grade 10", SortOrder = 4, IsActive = true },
            new GradeLevel { Code = "11", Name = "Grade 11", SortOrder = 5, IsActive = true },
            new GradeLevel { Code = "12", Name = "Grade 12", SortOrder = 6, IsActive = true }
        };

        context.GradeLevels.AddRange(gradeLevels);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} grade levels (7-12)", gradeLevels.Count);
    }

    private static async Task SeedAcademicYearsAsync(ApplicationDbContext context, ILogger logger)
    {
        // Only seed if no academic years exist
        if (context.AcademicYears.Any())
        {
            logger.LogInformation("Academic years already seeded, skipping");
            return;
        }

        var currentYear = DateTime.UtcNow.Year;
        var academicYears = new List<AcademicYear>
        {
            new AcademicYear
            {
                Name = $"{currentYear - 1}-{currentYear}",
                StartDate = new DateTime(currentYear - 1, 8, 1),
                EndDate = new DateTime(currentYear, 5, 31),
                IsCurrent = false,
                IsActive = true
            },
            new AcademicYear
            {
                Name = $"{currentYear}-{currentYear + 1}",
                StartDate = new DateTime(currentYear, 8, 1),
                EndDate = new DateTime(currentYear + 1, 5, 31),
                IsCurrent = true, // Set current academic year
                IsActive = true
            }
        };

        context.AcademicYears.AddRange(academicYears);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} academic years. Current: {Current}",
            academicYears.Count, academicYears.First(ay => ay.IsCurrent).Name);
    }

    private static async Task SeedSectionsAsync(ApplicationDbContext context, ILogger logger)
    {
        // Only seed if no sections exist
        if (context.Sections.Any())
        {
            logger.LogInformation("Sections already seeded, skipping");
            return;
        }

        var gradeLevels = await context.GradeLevels.OrderBy(gl => gl.SortOrder).ToListAsync();
        if (!gradeLevels.Any())
        {
            logger.LogWarning("No grade levels found, cannot seed sections");
            return;
        }

        var sections = new List<Section>();
        var sectionNames = new[] { "A", "B", "C" };

        foreach (var gradeLevel in gradeLevels)
        {
            foreach (var sectionName in sectionNames)
            {
                sections.Add(new Section
                {
                    Name = sectionName,
                    GradeLevelId = gradeLevel.Id,
                    Capacity = 40,
                    IsActive = true,
                    AdviserId = null // Will be assigned later
                });
            }
        }

        context.Sections.AddRange(sections);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} sections (A, B, C for each grade)", sections.Count);
    }

    private static async Task MigrateExistingStudentsToEnrollmentsAsync(ApplicationDbContext context, ILogger logger)
    {
        // Find students who don't have enrollments yet
        var studentsWithoutEnrollments = await context.Students
            .Where(s => s.CurrentEnrollmentId == null)
            .ToListAsync();

        if (!studentsWithoutEnrollments.Any())
        {
            logger.LogInformation("No students need enrollment migration");
            return;
        }

        var currentAcademicYear = await context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.IsCurrent && ay.IsActive);

        if (currentAcademicYear == null)
        {
            logger.LogWarning("No current academic year found, cannot migrate student enrollments");
            return;
        }

        var gradeLevels = await context.GradeLevels.ToListAsync();
        var sections = await context.Sections
            .Include(s => s.GradeLevel)
            .ToListAsync();

        var enrollments = new List<StudentEnrollment>();
        int migratedCount = 0;
        int skippedCount = 0;

        foreach (var student in studentsWithoutEnrollments)
        {
            // Try to find matching grade level
            var gradeLevel = gradeLevels.FirstOrDefault(gl =>
                gl.Code.Equals(student.GradeLevel, StringComparison.OrdinalIgnoreCase));

            if (gradeLevel == null)
            {
                logger.LogWarning("Student {StudentId} has unknown grade level '{GradeLevel}', skipping",
                    student.StudentId, student.GradeLevel);
                skippedCount++;
                continue;
            }

            // Try to find matching section
            var section = sections.FirstOrDefault(s =>
                s.GradeLevelId == gradeLevel.Id &&
                s.Name.Equals(student.Section, StringComparison.OrdinalIgnoreCase));

            if (section == null)
            {
                // Create section if it doesn't exist
                section = new Section
                {
                    Name = student.Section,
                    GradeLevelId = gradeLevel.Id,
                    Capacity = 40,
                    IsActive = true
                };
                context.Sections.Add(section);
                await context.SaveChangesAsync(); // Save to get ID

                logger.LogInformation("Created new section: {Grade} - {Section}",
                    gradeLevel.Name, section.Name);
            }

            // Create enrollment
            var enrollment = new StudentEnrollment
            {
                StudentId = student.Id,
                SectionId = section.Id,
                AcademicYearId = currentAcademicYear.Id,
                EnrolledAt = student.CreatedAt,
                IsActive = true
            };

            enrollments.Add(enrollment);
            migratedCount++;
        }

        if (enrollments.Any())
        {
            context.StudentEnrollments.AddRange(enrollments);
            await context.SaveChangesAsync();

            // Update students' CurrentEnrollmentId
            foreach (var enrollment in enrollments)
            {
                var student = studentsWithoutEnrollments.First(s => s.Id == enrollment.StudentId);
                student.CurrentEnrollmentId = enrollment.Id;
            }

            await context.SaveChangesAsync();

            logger.LogInformation("Migrated {MigratedCount} students to enrollments, skipped {SkippedCount}",
                migratedCount, skippedCount);
        }
    }

    private static async Task SeedCalendarEventsAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.CalendarEvents.AnyAsync())
        {
            logger.LogInformation("Calendar events already exist, skipping seeding");
            return;
        }

        // Get the current academic year
        var currentAcademicYear = await context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.IsCurrent);

        if (currentAcademicYear == null)
        {
            logger.LogWarning("No current academic year found, skipping calendar event seeding");
            return;
        }

        // Get a super admin user for CreatedBy
        var superAdmin = await context.Users
            .FirstOrDefaultAsync(u => u.UserName == "super.admin");

        if (superAdmin == null)
        {
            logger.LogWarning("Super admin user not found, skipping calendar event seeding");
            return;
        }

        var year = currentAcademicYear.StartDate.Year;

        // Philippine National Holidays for the current academic year
        var holidays = new List<CalendarEvent>
        {
            new()
            {
                Title = "New Year's Day",
                Description = "Celebration of the first day of the year",
                EventType = EventType.Holiday,
                Category = "National",
                StartDate = new DateTime(year, 1, 1),
                EndDate = new DateTime(year, 1, 1),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "EDSA People Power Revolution Anniversary",
                Description = "Commemoration of the 1986 People Power Revolution",
                EventType = EventType.Holiday,
                Category = "National",
                StartDate = new DateTime(year, 2, 25),
                EndDate = new DateTime(year, 2, 25),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "Araw ng Kagitingan (Bataan Day)",
                Description = "Day of Valor, honors Filipino and American soldiers",
                EventType = EventType.Holiday,
                Category = "National",
                StartDate = new DateTime(year, 4, 9),
                EndDate = new DateTime(year, 4, 9),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "Maundy Thursday",
                Description = "Holy Week observance",
                EventType = EventType.Holiday,
                Category = "Religious",
                StartDate = new DateTime(year, 4, 17),
                EndDate = new DateTime(year, 4, 17),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "Good Friday",
                Description = "Holy Week observance",
                EventType = EventType.Holiday,
                Category = "Religious",
                StartDate = new DateTime(year, 4, 18),
                EndDate = new DateTime(year, 4, 18),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "Labor Day",
                Description = "International Workers' Day",
                EventType = EventType.Holiday,
                Category = "National",
                StartDate = new DateTime(year, 5, 1),
                EndDate = new DateTime(year, 5, 1),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "Independence Day",
                Description = "Philippine Declaration of Independence from Spain",
                EventType = EventType.Holiday,
                Category = "National",
                StartDate = new DateTime(year, 6, 12),
                EndDate = new DateTime(year, 6, 12),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "Ninoy Aquino Day",
                Description = "Commemoration of Benigno Aquino Jr.'s assassination",
                EventType = EventType.Holiday,
                Category = "National",
                StartDate = new DateTime(year, 8, 21),
                EndDate = new DateTime(year, 8, 21),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "National Heroes Day",
                Description = "Honors Filipino heroes and patriots",
                EventType = EventType.Holiday,
                Category = "National",
                StartDate = new DateTime(year, 8, 25),
                EndDate = new DateTime(year, 8, 25),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "All Saints' Day",
                Description = "Day to honor all Christian saints",
                EventType = EventType.Holiday,
                Category = "Religious",
                StartDate = new DateTime(year, 11, 1),
                EndDate = new DateTime(year, 11, 1),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "Bonifacio Day",
                Description = "Birthday of Andres Bonifacio, Katipunan founder",
                EventType = EventType.Holiday,
                Category = "National",
                StartDate = new DateTime(year, 11, 30),
                EndDate = new DateTime(year, 11, 30),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "Christmas Day",
                Description = "Celebration of the birth of Jesus Christ",
                EventType = EventType.Holiday,
                Category = "Religious",
                StartDate = new DateTime(year, 12, 25),
                EndDate = new DateTime(year, 12, 25),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            },
            new()
            {
                Title = "Rizal Day",
                Description = "Commemorates the execution of José Rizal",
                EventType = EventType.Holiday,
                Category = "National",
                StartDate = new DateTime(year, 12, 30),
                EndDate = new DateTime(year, 12, 30),
                IsAllDay = true,
                AffectsAttendance = true,
                AffectsClasses = true,
                AcademicYearId = currentAcademicYear.Id,
                CreatedBy = superAdmin.Id,
                IsActive = true
            }
        };

        // Add holidays for next year if the academic year spans two calendar years
        if (currentAcademicYear.EndDate.Year > year)
        {
            var nextYear = year + 1;
            holidays.AddRange(new[]
            {
                new CalendarEvent
                {
                    Title = "New Year's Day",
                    Description = "Celebration of the first day of the year",
                    EventType = EventType.Holiday,
                    Category = "National",
                    StartDate = new DateTime(nextYear, 1, 1),
                    EndDate = new DateTime(nextYear, 1, 1),
                    IsAllDay = true,
                    AffectsAttendance = true,
                    AffectsClasses = true,
                    AcademicYearId = currentAcademicYear.Id,
                    CreatedBy = superAdmin.Id,
                    IsActive = true
                }
            });
        }

        context.CalendarEvents.AddRange(holidays);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} calendar events (Philippine holidays)", holidays.Count);
    }

    private static async Task SeedSmsTemplatesAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.SmsTemplates.AnyAsync())
        {
            logger.LogInformation("SMS templates already exist, skipping seeding");
            return;
        }

        var templates = new List<SmsTemplate>
        {
            new()
            {
                Code = "ENTRY",
                Name = "Student Entry Notification",
                TemplateEn = "[SmartLog] {StudentName} ({Grade}-{Section}) entered school at {Time}.",
                TemplateFil = "[SmartLog] Si {StudentName} ({Grade}-{Section}) ay pumasok sa eskwelahan ng {Time}.",
                AvailablePlaceholders = "{StudentName},{Grade},{Section},{Time}",
                IsActive = true,
                IsSystem = true
            },
            new()
            {
                Code = "EXIT",
                Name = "Student Exit Notification",
                TemplateEn = "[SmartLog] {StudentName} ({Grade}-{Section}) left school at {Time}.",
                TemplateFil = "[SmartLog] Si {StudentName} ({Grade}-{Section}) ay umalis sa eskwelahan ng {Time}.",
                AvailablePlaceholders = "{StudentName},{Grade},{Section},{Time}",
                IsActive = true,
                IsSystem = true
            },
            new()
            {
                Code = "HOLIDAY",
                Name = "Holiday Announcement",
                TemplateEn = "[SmartLog] Reminder: No classes on {Date} - {EventTitle}.",
                TemplateFil = "[SmartLog] Paalala: Walang pasok sa {Date} - {EventTitle}.",
                AvailablePlaceholders = "{Date},{EventTitle}",
                IsActive = true,
                IsSystem = true
            },
            new()
            {
                Code = "SUSPENSION",
                Name = "Class Suspension Notice",
                TemplateEn = "[SmartLog] Classes suspended on {Date}: {EventTitle}.",
                TemplateFil = "[SmartLog] Suspendido ang klase sa {Date}: {EventTitle}.",
                AvailablePlaceholders = "{Date},{EventTitle}",
                IsActive = true,
                IsSystem = true
            },
            new()
            {
                Code = "EMERGENCY",
                Name = "Emergency Alert",
                TemplateEn = "[SmartLog ALERT] {Message}",
                TemplateFil = "[SmartLog ALERTO] {Message}",
                AvailablePlaceholders = "{Message}",
                IsActive = true,
                IsSystem = true
            }
        };

        context.SmsTemplates.AddRange(templates);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} SMS templates (ENTRY, EXIT, HOLIDAY, SUSPENSION, EMERGENCY)", templates.Count);
    }
}
