using Microsoft.AspNetCore.Identity;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Middleware;

public class ForcePasswordChangeMiddleware
{
    private readonly RequestDelegate _next;

    public ForcePasswordChangeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Allow access to change password, logout, and static files
            if (!path.StartsWith("/account/changepassword") &&
                !path.StartsWith("/account/logout") &&
                !path.StartsWith("/css/") &&
                !path.StartsWith("/js/") &&
                !path.StartsWith("/lib/") &&
                !path.StartsWith("/_framework/") &&
                !path.EndsWith(".css") &&
                !path.EndsWith(".js") &&
                !path.EndsWith(".ico"))
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user != null && user.MustChangePassword)
                {
                    context.Response.Redirect("/Account/ChangePassword");
                    return;
                }
            }
        }

        await _next(context);
    }
}
