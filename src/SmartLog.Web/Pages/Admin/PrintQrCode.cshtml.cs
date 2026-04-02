using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Print QR code page for individual student.
/// Implements US0021 (Print Individual QR Code).
/// </summary>
[Authorize(Policy = "CanManageStudents")]
public class PrintQrCodeModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public PrintQrCodeModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public Student Student { get; set; } = null!;
    public QrCode QrCode { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var student = await _context.Students
            .Include(s => s.QrCode)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            return NotFound();
        }

        if (student.QrCode == null || !student.QrCode.IsValid)
        {
            return BadRequest("No valid QR code available for this student");
        }

        Student = student;
        QrCode = student.QrCode;

        return Page();
    }
}
