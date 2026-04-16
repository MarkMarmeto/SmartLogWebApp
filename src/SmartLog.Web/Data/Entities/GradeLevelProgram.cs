namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Junction table — many-to-many between GradeLevel and Program.
/// Determines which programs are available for a given grade level.
/// </summary>
public class GradeLevelProgram
{
    public Guid GradeLevelId { get; set; }
    public Guid ProgramId { get; set; }

    // Navigation properties
    public virtual GradeLevel GradeLevel { get; set; } = null!;
    public virtual Program Program { get; set; } = null!;
}
