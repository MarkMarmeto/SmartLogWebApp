using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Models;

public class StudentIdCardViewModel
{
    public Student Student { get; init; } = null!;
    public QrCode QrCode { get; init; } = null!;
    public string SchoolName { get; init; } = "SmartLog School";
    public string? SchoolAddress { get; init; }
    public string? SchoolLogoPath { get; init; }
    public string? ReturnAddressText { get; init; }
}
