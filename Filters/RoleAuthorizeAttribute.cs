using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Reservations.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Reservations.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RoleAuthorizeAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string[] _roles;

        public RoleAuthorizeAttribute(params string[] roles)
        {
            _roles = roles ?? Array.Empty<string>();
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            var sess = http.Session;
            var userId = sess.GetInt32("UserId");

            if (userId == null)
            {
                // not logged in
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = http.Request.Path + http.Request.QueryString });
                return;
            }

            // Resolve DB and user with role
            var db = http.RequestServices.GetService(typeof(ReservationsDbContext)) as ReservationsDbContext;
            if (db == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (user == null)
            {
                // session references missing user
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = http.Request.Path + http.Request.QueryString });
                return;
            }

            var userRoleName = user.Role?.Name;
            if (string.IsNullOrEmpty(userRoleName))
            {
                context.Result = new ForbidResult();
                return;
            }

            if (_roles == null || _roles.Length == 0)
            {
                context.Result = new ForbidResult();
                return;
            }

            // Check if user's role name matches any of allowed roles (case-insensitive)
            if (!_roles.Any(r => string.Equals(r, userRoleName, StringComparison.OrdinalIgnoreCase)))
            {
                context.Result = new ForbidResult();
                return;
            }

            await next();
        }
    }
}
