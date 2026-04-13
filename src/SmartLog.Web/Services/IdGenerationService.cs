using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;

namespace SmartLog.Web.Services;

/// <summary>
/// Implementation of ID generation service for students and employees.
/// Uses application-level locking to prevent duplicate IDs during concurrent operations.
/// Does NOT create its own transaction — callers (e.g., BulkImportService) are responsible
/// for wrapping calls in a transaction if atomicity across multiple operations is needed.
/// </summary>
public class IdGenerationService : IIdGenerationService
{
    private readonly ApplicationDbContext _context;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ILogger<IdGenerationService> _logger;

    // Application-level locks to serialize ID generation within this process
    private static readonly SemaphoreSlim _studentIdLock = new(1, 1);
    private static readonly SemaphoreSlim _employeeIdLock = new(1, 1);

    public IdGenerationService(
        ApplicationDbContext context,
        IAppSettingsService appSettingsService,
        ILogger<IdGenerationService> logger)
    {
        _context = context;
        _appSettingsService = appSettingsService;
        _logger = logger;
    }

    public async Task<string> GenerateStudentIdAsync()
    {
        var year = DateTime.UtcNow.Year;

        // Get school code from settings
        var schoolCode = await _appSettingsService.GetAsync("System.SchoolCode") ?? "SL";
        schoolCode = schoolCode.Trim().ToUpperInvariant();

        var pattern = $"{schoolCode}-{year}-";

        // Serialize access to prevent duplicate IDs from concurrent requests
        await _studentIdLock.WaitAsync();
        try
        {
            // Find the highest sequence number for this year
            var existingIds = await _context.Students
                .Where(s => s.StudentId.StartsWith(pattern))
                .Select(s => s.StudentId)
                .ToListAsync();

            int maxSequence = 0;

            foreach (var id in existingIds)
            {
                var lastHyphen = id.LastIndexOf('-');
                if (lastHyphen >= 0 && int.TryParse(id.Substring(lastHyphen + 1), out int sequence))
                {
                    maxSequence = Math.Max(maxSequence, sequence);
                }
            }

            var newSequence = maxSequence + 1;
            var studentId = $"{schoolCode}-{year}-{newSequence:D5}";

            _logger.LogInformation("Generated Student ID: {StudentId} (SchoolCode: {SchoolCode}, Sequence: {Sequence})",
                studentId, schoolCode, newSequence);

            return studentId;
        }
        finally
        {
            _studentIdLock.Release();
        }
    }

    public async Task<string> GenerateEmployeeIdAsync()
    {
        var year = DateTime.UtcNow.Year;
        var pattern = $"EMP-{year}-";

        await _employeeIdLock.WaitAsync();
        try
        {
            var existingIds = await _context.Faculties
                .Where(f => f.EmployeeId.StartsWith(pattern))
                .Select(f => f.EmployeeId)
                .ToListAsync();

            int maxSequence = 0;

            foreach (var id in existingIds)
            {
                var parts = id.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int sequence))
                {
                    maxSequence = Math.Max(maxSequence, sequence);
                }
            }

            var newSequence = maxSequence + 1;
            var employeeId = $"EMP-{year}-{newSequence:D4}";

            _logger.LogInformation("Generated Employee ID: {EmployeeId} (Sequence: {Sequence})",
                employeeId, newSequence);

            return employeeId;
        }
        finally
        {
            _employeeIdLock.Release();
        }
    }
}
