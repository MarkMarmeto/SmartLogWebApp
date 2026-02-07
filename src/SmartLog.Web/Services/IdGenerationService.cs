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
    private readonly ILogger<IdGenerationService> _logger;

    public IdGenerationService(ApplicationDbContext context, ILogger<IdGenerationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GenerateStudentIdAsync(string gradeCode)
    {
        // Get current year from Philippines timezone would be ideal, but for ID generation
        // we'll use UTC year as it's simpler and consistent
        var year = DateTime.UtcNow.Year;

        // Format grade code to 2 digits, or use 'K' for Kindergarten
        string gradeFormatted;
        if (gradeCode.Equals("K", StringComparison.OrdinalIgnoreCase))
        {
            gradeFormatted = "K";
        }
        else if (int.TryParse(gradeCode, out int gradeNum))
        {
            gradeFormatted = gradeNum.ToString("00"); // 2-digit padding: 01, 02, ..., 12
        }
        else
        {
            throw new ArgumentException($"Invalid grade code: {gradeCode}. Must be 'K' or a number 1-12.");
        }

        // Pattern for matching existing IDs for this year and grade
        var pattern = $"{year}-{gradeFormatted}-";

        // Use a transaction to ensure thread-safety
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Find the highest sequence number for this year and grade
            var existingIds = await _context.Students
                .Where(s => s.StudentId.StartsWith(pattern))
                .Select(s => s.StudentId)
                .ToListAsync();

            int maxSequence = 0;

            if (existingIds.Any())
            {
                foreach (var id in existingIds)
                {
                    // Extract the sequence number (last 4 digits)
                    var parts = id.Split('-');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int sequence))
                    {
                        maxSequence = Math.Max(maxSequence, sequence);
                    }
                }
            }

            // Increment for new ID
            var newSequence = maxSequence + 1;

            // Format: YYYY-GG-NNNN (4-digit sequence number)
            var studentId = $"{year}-{gradeFormatted}-{newSequence:D4}";

            await transaction.CommitAsync();

            _logger.LogInformation("Generated Student ID: {StudentId} (Grade: {Grade}, Sequence: {Sequence})",
                studentId, gradeCode, newSequence);

            return studentId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error generating Student ID for grade {Grade}", gradeCode);
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

            if (existingIds.Any())
            {
                foreach (var id in existingIds)
                {
                    // Extract the sequence number (last 4 digits)
                    var parts = id.Split('-');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int sequence))
                    {
                        maxSequence = Math.Max(maxSequence, sequence);
                    }
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
