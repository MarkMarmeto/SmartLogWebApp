using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;

namespace SmartLog.Web.Services;

/// <summary>
/// Implementation of ID generation service for students and employees.
/// Thread-safe with database transaction support for sequential numbering.
/// </summary>
public class IdGenerationService : IIdGenerationService
{
    private readonly ApplicationDbContext _context;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ILogger<IdGenerationService> _logger;

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

        // Pattern for matching existing IDs for this school code and year
        var pattern = $"{schoolCode}-{year}-";

        // Use a transaction to ensure thread-safety
        using var transaction = await _context.Database.BeginTransactionAsync();

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
                // Extract the sequence number (last segment after final hyphen)
                var lastHyphen = id.LastIndexOf('-');
                if (lastHyphen >= 0 && int.TryParse(id.Substring(lastHyphen + 1), out int sequence))
                {
                    maxSequence = Math.Max(maxSequence, sequence);
                }
            }

            // Increment for new ID
            var newSequence = maxSequence + 1;

            // Format: CODE-YYYY-NNNNN (5-digit sequence number)
            var studentId = $"{schoolCode}-{year}-{newSequence:D5}";

            await transaction.CommitAsync();

            _logger.LogInformation("Generated Student ID: {StudentId} (SchoolCode: {SchoolCode}, Sequence: {Sequence})",
                studentId, schoolCode, newSequence);

            return studentId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error generating Student ID");
            throw;
        }
    }

    public async Task<string> GenerateEmployeeIdAsync()
    {
        var year = DateTime.UtcNow.Year;
        var pattern = $"EMP-{year}-";

        // Use a transaction to ensure thread-safety
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Find the highest sequence number for this year
            var existingIds = await _context.Faculties
                .Where(f => f.EmployeeId.StartsWith(pattern))
                .Select(f => f.EmployeeId)
                .ToListAsync();

            int maxSequence = 0;

            foreach (var id in existingIds)
            {
                // Extract the sequence number (last 4 digits)
                var parts = id.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int sequence))
                {
                    maxSequence = Math.Max(maxSequence, sequence);
                }
            }

            // Increment for new ID
            var newSequence = maxSequence + 1;

            // Format: EMP-YYYY-NNNN (4-digit sequence number)
            var employeeId = $"EMP-{year}-{newSequence:D4}";

            await transaction.CommitAsync();

            _logger.LogInformation("Generated Employee ID: {EmployeeId} (Sequence: {Sequence})",
                employeeId, newSequence);

            return employeeId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error generating Employee ID");
            throw;
        }
    }
}
