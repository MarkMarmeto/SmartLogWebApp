using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Middleware;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Branding;
using SmartLog.Web.Services.Retention;
using SmartLog.Web.Services.Sms;

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting SmartLog Web Application");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from configuration
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // Enable running as a Windows Service (no-op when running as console app)
    builder.Host.UseWindowsService();

    // Add DbContext with SQL Server
    // Prefer environment variable, then fall back to configuration
    var connectionString = Environment.GetEnvironmentVariable("SMARTLOG_DB_CONNECTION")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Set SMARTLOG_DB_CONNECTION environment variable.");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    // Add ASP.NET Identity with custom ApplicationUser
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password requirements (per stakeholder decisions)
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // Lockout settings (for US0002, configured here for consistency)
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;

        // Sign-in settings
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // Configure cookie authentication
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(10); // 10-hour session per stakeholder decision
        options.SlidingExpiration = true;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

    // Add memory cache
    builder.Services.AddMemoryCache();

    // Add application services
    builder.Services.AddScoped<IAppSettingsService, AppSettingsService>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<IQrCodeService, QrCodeService>();
    builder.Services.AddScoped<IDeviceService, DeviceService>();
    builder.Services.AddScoped<IDeviceHealthService, DeviceHealthService>();
    builder.Services.AddScoped<IAttendanceService, AttendanceService>();
    builder.Services.AddScoped<IReportExportService, ReportExportService>();
    builder.Services.AddScoped<IFileUploadService, FileUploadService>();
    builder.Services.AddScoped<IBrandingService, BrandingService>();
    builder.Services.AddScoped<ITimezoneService, TimezoneService>();
    builder.Services.AddScoped<IAcademicYearService, AcademicYearService>();
    builder.Services.AddScoped<IGradeSectionService, GradeSectionService>();
    builder.Services.AddScoped<IIdGenerationService, IdGenerationService>();
    builder.Services.AddScoped<ICalendarService, CalendarService>();
    builder.Services.AddScoped<IBulkImportService, BulkImportService>();
    builder.Services.AddScoped<IBatchReenrollmentService, BatchReenrollmentService>();
    builder.Services.AddScoped<IDashboardService, DashboardService>();
    builder.Services.AddScoped<IVisitorPassService, VisitorPassService>();

    // Add SMS services
    builder.Services.AddScoped<ISmsSettingsService, SmsSettingsService>();
    builder.Services.AddScoped<ISmsTemplateService, SmsTemplateService>();
    builder.Services.AddScoped<ISmsService, SmsService>();
    builder.Services.AddSingleton<GsmModemGateway>();
    builder.Services.AddSingleton<SemaphoreGateway>();
    builder.Services.AddHostedService<SmsWorkerService>();
    builder.Services.AddSingleton<NoScanAlertService>();
    builder.Services.AddSingleton<INoScanAlertService>(sp => sp.GetRequiredService<NoScanAlertService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<NoScanAlertService>());
    builder.Services.AddHttpClient(); // Required for SemaphoreGateway

    // Add retention handlers (EP0017)
    builder.Services.AddScoped<IEntityRetentionHandler, SmsQueueRetentionHandler>();
    builder.Services.AddScoped<IEntityRetentionHandler, SmsLogRetentionHandler>();
    builder.Services.AddScoped<IEntityRetentionHandler, BroadcastRetentionHandler>();
    builder.Services.AddScoped<IEntityRetentionHandler, ScanRetentionHandler>();
    builder.Services.AddScoped<IEntityRetentionHandler, AuditLogRetentionHandler>();
    builder.Services.AddScoped<IEntityRetentionHandler, VisitorScanRetentionHandler>();
    builder.Services.AddHostedService<RetentionService>();
    builder.Services.AddScoped<IArchiveService, CsvArchiveService>();

    // Add authorization policies (US0007)
    builder.Services.AddAuthorization(options =>
    {
        // SuperAdmin-only policies
        options.AddPolicy("RequireSuperAdmin", policy =>
            policy.RequireRole("SuperAdmin"));

        // Admin-level policies (SuperAdmin or Admin)
        options.AddPolicy("RequireAdmin", policy =>
            policy.RequireRole("SuperAdmin", "Admin"));

        // User management (SuperAdmin or Admin)
        options.AddPolicy("CanManageUsers", policy =>
            policy.RequireRole("SuperAdmin", "Admin"));

        // Student management
        options.AddPolicy("CanViewStudents", policy =>
            policy.RequireRole("SuperAdmin", "Admin", "Teacher", "Staff"));
        options.AddPolicy("CanManageStudents", policy =>
            policy.RequireRole("SuperAdmin", "Admin"));

        // Faculty management
        options.AddPolicy("CanViewFaculty", policy =>
            policy.RequireRole("SuperAdmin", "Admin", "Teacher"));
        options.AddPolicy("CanManageFaculty", policy =>
            policy.RequireRole("SuperAdmin", "Admin"));

        // Audit log access (SuperAdmin only)
        options.AddPolicy("CanViewAuditLogs", policy =>
            policy.RequireRole("SuperAdmin"));

        // Settings access (SuperAdmin and Admin)
        options.AddPolicy("CanManageSettings", policy =>
            policy.RequireRole("SuperAdmin", "Admin"));

        // Attendance (Phase 2)
        options.AddPolicy("CanViewAttendance", policy =>
            policy.RequireRole("SuperAdmin", "Admin", "Teacher", "Security"));
    });

    // Add CORS for scanner devices
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[] { "http://localhost:5050", "https://localhost:5051" };
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ScannerDevices", policy =>
            policy.WithOrigins(allowedOrigins)
                  .WithMethods("GET", "POST")
                  .AllowAnyHeader());
    });

    // Add Razor Pages
    builder.Services.AddRazorPages();
    builder.Services.AddControllers(); // For REST API endpoints

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>();

    // Configure HTTPS if a certificate is provisioned
    // Set SMARTLOG_CERT_PATH and SMARTLOG_CERT_PASSWORD env vars via Setup-Https.ps1
    var certPath = Environment.GetEnvironmentVariable("SMARTLOG_CERT_PATH")
                   ?? Path.Combine("C:\\SmartLog", "smartlog.pfx");
    var certPassword = Environment.GetEnvironmentVariable("SMARTLOG_CERT_PASSWORD");

    if (File.Exists(certPath) && !string.IsNullOrEmpty(certPassword))
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5050);  // HTTP — redirects to HTTPS
            options.ListenAnyIP(5051, listenOptions =>
            {
                listenOptions.UseHttps(certPath, certPassword);
            });
        });
        Log.Information("HTTPS enabled — certificate loaded from {CertPath}", certPath);
    }
    else
    {
        Log.Warning("HTTPS certificate not found — running HTTP only. Run deploy/Setup-Https.ps1 to enable HTTPS.");
    }

    var app = builder.Build();

    // Apply migrations and seed data
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();

            // Apply EF Core migrations automatically
            logger.LogInformation("Applying database migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully");

            // Seed data
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            await DbInitializer.SeedAsync(userManager, roleManager, context, logger);

            // Seed app settings defaults
            var appSettingsService = services.GetRequiredService<IAppSettingsService>();
            await appSettingsService.SeedDefaultsAsync();

            // Seed SMS settings defaults (only if not already set)
            var smsSettingsService = services.GetRequiredService<ISmsSettingsService>();
            var existingProvider = await smsSettingsService.GetSettingAsync("Sms.DefaultProvider");
            if (existingProvider == null)
            {
                await smsSettingsService.SetSettingAsync("Sms.DefaultProvider", "SEMAPHORE", "General");
                await smsSettingsService.SetSettingAsync("Sms.FallbackEnabled", "true", "General");
                logger.LogInformation("Seeded SMS defaults: DefaultProvider=SEMAPHORE, FallbackEnabled=true");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating or seeding the database");
            // In development, we might want to continue; in production, we should fail
            if (!app.Environment.IsDevelopment())
            {
                throw;
            }
        }
    }

    // Configure the HTTP request pipeline
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseStatusCodePagesWithReExecute("/errors/{0}");
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    // Security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        await next();
    });

    app.UseStaticFiles();

    // Add cache control headers to prevent back button showing cached pages
    app.Use(async (context, next) =>
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
        await next();
    });

    app.UseRouting();

    app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();

    // Force password change for new accounts and admin resets
    app.UseMiddleware<ForcePasswordChangeMiddleware>();

    app.UseSerilogRequestLogging();

    app.MapRazorPages();
    app.MapControllers(); // Map API controllers
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make the implicit Program class public for testing
public partial class Program { }
