using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Implementation of academic year management service.
/// </summary>
public class AcademicYearService : IAcademicYearService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AcademicYearService> _logger;

    public AcademicYearService(ApplicationDbContext context, ILogger<AcademicYearService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AcademicYear?> GetCurrentAcademicYearAsync()
    {
        return await _context.AcademicYears
            .Where(ay => ay.IsCurrent && ay.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AcademicYear>> GetAllAcademicYearsAsync(bool activeOnly = false)
    {
        var query = _context.AcademicYears.AsQueryable();

        if (activeOnly)
        {
            query = query.Where(ay => ay.IsActive);
        }

        return await query
            .OrderByDescending(ay => ay.StartDate)
            .ToListAsync();
    }

    public async Task<AcademicYear?> GetAcademicYearByIdAsync(Guid id)
    {
        return await _context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.Id == id);
    }

    public async Task<AcademicYear> CreateAcademicYearAsync(string name, DateTime startDate, DateTime endDate, bool setCurrent = false)
    {
        // Check if academic year with same name already exists
        var exists = await _context.AcademicYears
            .AnyAsync(ay => ay.Name == name);

        if (exists)
        {
            throw new InvalidOperationException($"Academic year '{name}' already exists.");
        }

        var academicYear = new AcademicYear
        {
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            IsCurrent = setCurrent,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // If setting as current, unset all other current flags
        if (setCurrent)
        {
            await UnsetAllCurrentFlagsAsync();
        }

        _context.AcademicYears.Add(academicYear);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created academic year: {Name} (ID: {Id}){Current}",
            name, academicYear.Id, setCurrent ? " - Set as current" : "");

        return academicYear;
    }

    public async Task SetCurrentAcademicYearAsync(Guid academicYearId)
    {
        var academicYear = await _context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.Id == academicYearId);

        if (academicYear == null)
        {
            throw new InvalidOperationException($"Academic year with ID {academicYearId} not found.");
        }

        if (!academicYear.IsActive)
        {
            throw new InvalidOperationException($"Cannot set inactive academic year '{academicYear.Name}' as current.");
        }

        // Unset all current flags
        await UnsetAllCurrentFlagsAsync();

        // Set the new current
        academicYear.IsCurrent = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Set academic year '{Name}' (ID: {Id}) as current", academicYear.Name, academicYear.Id);
    }

    public async Task UpdateAcademicYearAsync(AcademicYear academicYear)
    {
        _context.AcademicYears.Update(academicYear);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated academic year: {Name} (ID: {Id})", academicYear.Name, academicYear.Id);
    }

    public async Task DeactivateAcademicYearAsync(Guid academicYearId)
    {
        var academicYear = await _context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.Id == academicYearId);

        if (academicYear == null)
        {
            throw new InvalidOperationException($"Academic year with ID {academicYearId} not found.");
        }

        if (academicYear.IsCurrent)
        {
            throw new InvalidOperationException($"Cannot deactivate the current academic year '{academicYear.Name}'. Set a different year as current first.");
        }

        academicYear.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deactivated academic year: {Name} (ID: {Id})", academicYear.Name, academicYear.Id);
    }

    private async Task UnsetAllCurrentFlagsAsync()
    {
        var currentYears = await _context.AcademicYears
            .Where(ay => ay.IsCurrent)
            .ToListAsync();

        foreach (var year in currentYears)
        {
            year.IsCurrent = false;
        }

        if (currentYears.Any())
        {
            await _context.SaveChangesAsync();
        }
    }
}
