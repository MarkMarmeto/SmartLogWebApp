using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SmartLog.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorsModel : PageModel
{
    public int HttpStatusCode { get; private set; }
    public string Title { get; private set; } = "Error";
    public string Description { get; private set; } = "Something went wrong.";
    public string Icon { get; private set; } = "bi-exclamation-circle";

    public void OnGet(int statusCode)
    {
        HttpStatusCode = statusCode;

        switch (statusCode)
        {
            case 404:
                Title = "Page Not Found";
                Description = "The page you're looking for doesn't exist or has been moved.";
                Icon = "bi-search";
                break;
            case 403:
                Title = "Access Denied";
                Description = "You don't have permission to view this page.";
                Icon = "bi-lock";
                break;
            case 401:
                Title = "Unauthorised";
                Description = "Please log in to access this page.";
                Icon = "bi-person-lock";
                break;
            default:
                Title = "Something Went Wrong";
                Description = $"An unexpected error occurred (HTTP {HttpStatusCode}).";
                Icon = "bi-exclamation-triangle";
                break;
        }
    }
}
