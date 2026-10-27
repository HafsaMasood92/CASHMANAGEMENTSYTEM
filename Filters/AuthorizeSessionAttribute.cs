using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CashManagementSystem.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeSessionAttribute : ActionFilterAttribute
    {
        public string? Roles { get; set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var userEmail = session.GetString("UserEmail");

            // 1. Check if authenticated
            if (string.IsNullOrEmpty(userEmail))
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
                {
                    context.Result = new UnauthorizedObjectResult(new { success = false, message = "Authentication required. Please sign in." });
                }
                else
                {
                    context.Result = new RedirectResult("/Account/Login");
                }
                return;
            }

            // 2. Check Role authorization if Roles are specified
            if (!string.IsNullOrWhiteSpace(Roles))
            {
                var userRole = session.GetString("UserRole") ?? "";
                var allowed = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                bool isAuthorized = allowed.Any(r =>
                {
                    if (string.Equals(r, userRole, StringComparison.OrdinalIgnoreCase)) return true;
                    // Aliases for Organization Admin
                    if ((r.Equals("Organization Admin", StringComparison.OrdinalIgnoreCase) || r.Equals("Organization Owner", StringComparison.OrdinalIgnoreCase)) &&
                        (userRole.Equals("Organization Admin", StringComparison.OrdinalIgnoreCase) || userRole.Equals("Organization Owner", StringComparison.OrdinalIgnoreCase)))
                        return true;
                    // Aliases for Accountant
                    if ((r.Equals("Accountant", StringComparison.OrdinalIgnoreCase) || r.Equals("School Accountant", StringComparison.OrdinalIgnoreCase) || r.Equals("Multi-Unit Accountant", StringComparison.OrdinalIgnoreCase)) &&
                        (userRole.Equals("Accountant", StringComparison.OrdinalIgnoreCase) || userRole.Equals("School Accountant", StringComparison.OrdinalIgnoreCase) || userRole.Equals("Multi-Unit Accountant", StringComparison.OrdinalIgnoreCase)))
                        return true;
                    // Aliases for User
                    if ((r.Equals("User", StringComparison.OrdinalIgnoreCase) || r.Equals("Single Unit User", StringComparison.OrdinalIgnoreCase)) &&
                        (userRole.Equals("User", StringComparison.OrdinalIgnoreCase) || userRole.Equals("Single Unit User", StringComparison.OrdinalIgnoreCase)))
                        return true;

                    return false;
                });

                if (!isAuthorized)
                {
                    if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Result = new ObjectResult(new { success = false, message = "Access denied. You do not have permission to perform this action." })
                        {
                            StatusCode = StatusCodes.Status403Forbidden
                        };
                    }
                    else
                    {
                        context.Result = new RedirectToActionResult("Index", "Home", null);
                    }
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}
