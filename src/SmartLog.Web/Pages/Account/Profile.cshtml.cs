using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Account;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileUploadService _fileUploadService;

    public ProfileModel(
        UserManager<ApplicationUser> userManager,
        IFileUploadService fileUploadService)
    {
        _userManager = userManager;
        _fileUploadService = fileUploadService;
    }

    public string FullName { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public string ProfilePictureUrl { get; private set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            FullName = user.FullName;
            Username = user.UserName ?? string.Empty;
            Email = user.Email ?? string.Empty;
            IsActive = user.IsActive;
            ProfilePictureUrl = _fileUploadService.GetProfilePictureUrl(user.ProfilePicturePath);

            var roles = await _userManager.GetRolesAsync(user);
            Role = roles.FirstOrDefault() ?? "User";
        }
    }
}
