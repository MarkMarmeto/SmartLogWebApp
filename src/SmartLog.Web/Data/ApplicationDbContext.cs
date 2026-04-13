using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Data;

/// <summary>
/// Entity Framework Core database context for SmartLog.
/// Extends IdentityDbContext to include ASP.NET Identity tables.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<QrCode> QrCodes => Set<QrCode>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<StudentEnrollment> StudentEnrollments => Set<StudentEnrollment>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<SmsQueue> SmsQueues => Set<SmsQueue>();
    public DbSet<SmsLog> SmsLogs => Set<SmsLog>();
    public DbSet<SmsSettings> SmsSettings => Set<SmsSettings>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure ApplicationUser
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure AuditLog
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Action);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.PerformedByUser)
                .WithMany()
                .HasForeignKey(e => e.PerformedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure Student
        builder.Entity<Student>(entity =>
        {
            entity.HasIndex(e => e.StudentId).IsUnique();
            entity.HasIndex(e => e.LRN).IsUnique().HasFilter("[LRN] IS NOT NULL");
            entity.HasIndex(e => new { e.GradeLevel, e.Section });
            entity.HasIndex(e => e.LastName);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure QrCode
        builder.Entity<QrCode>(entity =>
        {
            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => new { e.IsValid, e.StudentId });

            entity.HasOne(e => e.Student)
                .WithOne(s => s.QrCode)
                .HasForeignKey<QrCode>(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.IssuedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Faculty
        builder.Entity<Faculty>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId).IsUnique();
            entity.HasIndex(e => e.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
            entity.HasIndex(e => e.Department);
            entity.HasIndex(e => e.LastName);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Device
        builder.Entity<Device>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.ApiKeyHash);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.RegisteredByUser)
                .WithMany()
                .HasForeignKey(e => e.RegisteredBy)
                .OnDelete(DeleteBehavior.NoAction);

            entity.Property(e => e.RegisteredAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Scan
        builder.Entity<Scan>(entity =>
        {
            entity.HasIndex(e => new { e.DeviceId, e.StudentId, e.ScannedAt });
            entity.HasIndex(e => e.ScannedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AcademicYearId);

            // Composite index for report queries filtering by status + date range
            entity.HasIndex(e => new { e.Status, e.ScannedAt })
                .HasDatabaseName("IX_Scans_Status_ScannedAt");

            // Unique filtered index to prevent concurrent duplicate scans
            // Only enforced for ACCEPTED scans (duplicates are allowed for rejected/duplicate statuses)
            entity.HasIndex(e => new { e.StudentId, e.ScanType, e.ScannedAt })
                .HasFilter("[Status] = 'ACCEPTED'")
                .IsUnique()
                .HasDatabaseName("IX_Scans_NoDuplicateAccepted");

            entity.HasOne(e => e.Device)
                .WithMany(d => d.Scans)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AcademicYear)
                .WithMany(a => a.Scans)
                .HasForeignKey(e => e.AcademicYearId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.ReceivedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure GradeLevel
        builder.Entity<GradeLevel>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.SortOrder);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Section
        builder.Entity<Section>(entity =>
        {
            entity.HasIndex(e => new { e.GradeLevelId, e.Name });
            entity.HasIndex(e => e.AdviserId);

            entity.HasOne(e => e.GradeLevel)
                .WithMany(g => g.Sections)
                .HasForeignKey(e => e.GradeLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Adviser)
                .WithMany(f => f.AdvisedSections)
                .HasForeignKey(e => e.AdviserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure AcademicYear
        builder.Entity<AcademicYear>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.IsCurrent);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure StudentEnrollment
        builder.Entity<StudentEnrollment>(entity =>
        {
            // Unique constraint: one active enrollment per student per academic year
            entity.HasIndex(e => new { e.StudentId, e.AcademicYearId })
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            entity.HasIndex(e => e.SectionId);
            entity.HasIndex(e => new { e.AcademicYearId, e.IsActive });

            entity.HasOne(e => e.Student)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Section)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AcademicYear)
                .WithMany(a => a.Enrollments)
                .HasForeignKey(e => e.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.EnrolledAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Student CurrentEnrollment relationship
        builder.Entity<Student>(entity =>
        {
            entity.HasOne(s => s.CurrentEnrollment)
                .WithMany()
                .HasForeignKey(s => s.CurrentEnrollmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure CalendarEvent
        builder.Entity<CalendarEvent>(entity =>
        {
            entity.HasIndex(e => new { e.StartDate, e.EndDate });
            entity.HasIndex(e => e.AcademicYearId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => new { e.AffectsAttendance, e.StartDate });

            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Organizer)
                .WithMany()
                .HasForeignKey(e => e.OrganizerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure SmsTemplate
        builder.Entity<SmsTemplate>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.IsActive);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Broadcast
        builder.Entity<Broadcast>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => e.Type);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure SmsQueue
        builder.Entity<SmsQueue>(entity =>
        {
            entity.HasIndex(e => new { e.Status, e.Priority, e.CreatedAt });
            entity.HasIndex(e => e.NextRetryAt);
            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => e.MessageType);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => e.BroadcastId);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Broadcast)
                .WithMany(b => b.Messages)
                .HasForeignKey(e => e.BroadcastId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure SmsLog
        builder.Entity<SmsLog>(entity =>
        {
            entity.HasIndex(e => e.QueueId);
            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.PhoneNumber, e.CreatedAt });
            entity.HasIndex(e => e.ProviderMessageId);

            entity.HasOne(e => e.Queue)
                .WithMany()
                .HasForeignKey(e => e.QueueId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure SmsSettings
        builder.Entity<SmsSettings>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.Category);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure AppSettings
        builder.Entity<AppSettings>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.Category);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var userEntries = ChangeTracker.Entries<ApplicationUser>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in userEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        var studentEntries = ChangeTracker.Entries<Student>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in studentEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        var facultyEntries = ChangeTracker.Entries<Faculty>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in facultyEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        var calendarEventEntries = ChangeTracker.Entries<CalendarEvent>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in calendarEventEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        var smsTemplateEntries = ChangeTracker.Entries<SmsTemplate>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in smsTemplateEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        var smsSettingsEntries = ChangeTracker.Entries<SmsSettings>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in smsSettingsEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        var broadcastEntries = ChangeTracker.Entries<Broadcast>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in broadcastEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
