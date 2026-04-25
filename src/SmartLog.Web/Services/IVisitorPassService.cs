using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for managing visitor passes and QR generation.
/// Implements US0072 (Visitor Pass Entity & QR Generation).
/// </summary>
public interface IVisitorPassService
{
    /// <summary>
    /// Generate passes up to the configured MaxPasses count.
    /// Creates only missing passes; existing passes are untouched.
    /// </summary>
    Task<List<VisitorPass>> GeneratePassesAsync();

    /// <summary>
    /// Get a visitor pass by its code (e.g., "VISITOR-005").
    /// </summary>
    Task<VisitorPass?> GetByCodeAsync(string code);

    /// <summary>
    /// Get all visitor passes ordered by PassNumber.
    /// </summary>
    Task<List<VisitorPass>> GetAllAsync();

    /// <summary>
    /// Deactivate a pass (sets IsActive=false, CurrentStatus=Deactivated).
    /// </summary>
    Task DeactivatePassAsync(Guid passId);

    /// <summary>
    /// Activate a previously deactivated pass (sets IsActive=true, CurrentStatus=Available).
    /// </summary>
    Task ActivatePassAsync(Guid passId);

    /// <summary>
    /// Sync pass count to match MaxPasses: generate new if increased, deactivate excess if decreased.
    /// </summary>
    Task SyncPassCountAsync();

    /// <summary>
    /// Get the configured maximum number of passes.
    /// </summary>
    Task<int> GetMaxPassesAsync();

    /// <summary>
    /// Set the maximum number of passes. Must be >= 1.
    /// </summary>
    Task SetMaxPassesAsync(int count);

    /// <summary>
    /// Get visitor scan log with ENTRY/EXIT pairing and duration calculation.
    /// </summary>
    Task<VisitorLogResult> GetVisitorLogAsync(DateTime? startDate, DateTime? endDate, string? passCodeFilter, int page, int pageSize, string? deviceFilter = null, string? cameraFilter = null);
}

/// <summary>
/// Result of visitor log query with paired visits and summary.
/// </summary>
public class VisitorLogResult
{
    public List<VisitorVisit> Visits { get; set; } = new();
    public int TotalCount { get; set; }
    public VisitorLogSummary Summary { get; set; } = new();
}

/// <summary>
/// A paired ENTRY/EXIT visit record.
/// </summary>
public class VisitorVisit
{
    public string PassCode { get; set; } = string.Empty;
    public int PassNumber { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public string Duration { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? CameraIndex { get; set; }
    public string? CameraName { get; set; }
}

/// <summary>
/// Summary statistics for visitor log.
/// </summary>
public class VisitorLogSummary
{
    public int TotalVisitors { get; set; }
    public string AvgDuration { get; set; } = "—";
    public int CurrentlyIn { get; set; }
}
