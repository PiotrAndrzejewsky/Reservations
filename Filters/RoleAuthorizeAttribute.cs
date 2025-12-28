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
            var roleId = sess.GetInt32("RoleId");

            if (userId == null || roleId == null)
            {
                // not logged in
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = http.Request.Path + http.Request.QueryString });
                return;
            }

            // Resolve allowed role ids from DB roles (safer than hardcoding)
            var db = http.RequestServices.GetService(typeof(ReservationsDbContext)) as ReservationsDbContext;
            if (db == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            var allowedRoleIds = new List<int>();
            foreach (var r in _roles)
            {
                var role = await db.Roles.FirstOrDefaultAsync(x => x.Name == r);
                if (role != null) allowedRoleIds.Add(role.Id);
            }

            if (!allowedRoleIds.Any())
            {
                // Nothing resolved -> deny
                context.Result = new ForbidResult();
                return;
            }

            if (!allowedRoleIds.Contains(roleId.Value))
            {
                context.Result = new ForbidResult();
                return;
            }

            await next();
        }
    }
}
