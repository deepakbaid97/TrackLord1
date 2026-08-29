using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using TrackLord.Domain.Aggregates.RoleAggregate;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Repositories;

namespace TrackLord.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequirePermissionAttribute : Attribute, IAsyncActionFilter
{
    public string Feature { get; }
    public string AccessLevel { get; }

    public RequirePermissionAttribute(string feature, string accessLevel)
    {
        Feature = feature;
        AccessLevel = accessLevel;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var claimsPrincipal = httpContext.User;

        if (claimsPrincipal == null || claimsPrincipal.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Authentication required." });
            return;
        }

        // Fast path: Check Admin role in claims
        var claimRole = claimsPrincipal.FindFirstValue(ClaimTypes.Role) ?? claimsPrincipal.FindFirstValue("role");
        if (string.Equals(claimRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // Resolve user repository and role repository
        var userRepo = httpContext.RequestServices.GetRequiredService<IUserRepository>();
        var roleRepo = httpContext.RequestServices.GetRequiredService<IRoleRepository>();

        var userIdStr = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? claimsPrincipal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? claimsPrincipal.FindFirstValue("sub");
        var userPhone = claimsPrincipal.FindFirstValue("phone_number")
                        ?? claimsPrincipal.FindFirstValue(ClaimTypes.MobilePhone);

        User? dbUser = null;
        if (Guid.TryParse(userIdStr, out var userGuid))
        {
            dbUser = await userRepo.GetByIdAsync(userGuid);
        }
        if (dbUser == null && !string.IsNullOrWhiteSpace(userPhone))
        {
            dbUser = await userRepo.GetByPhoneNumberAsync(userPhone);
        }

        if (dbUser == null || !dbUser.IsActive)
        {
            context.Result = new ObjectResult(new { message = "User account not found or is deactivated." })
            {
                StatusCode = 403
            };
            return;
        }

        // Admin role on DB entity check
        if (string.Equals(dbUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // Resolve Role entity
        Role? role = null;
        if (dbUser.RoleId.HasValue && dbUser.RoleId.Value != Guid.Empty)
        {
            role = await roleRepo.GetByIdAsync(dbUser.RoleId.Value);
        }

        if (role == null && !string.IsNullOrWhiteSpace(dbUser.Role))
        {
            role = await roleRepo.GetByNameAsync(dbUser.Role);
        }

        if (role == null || !role.IsActive)
        {
            context.Result = new ObjectResult(new { message = $"Assigned role ('{dbUser.Role}') not found or deactivated." })
            {
                StatusCode = 403
            };
            return;
        }

        if (!role.HasPermission(Feature, AccessLevel))
        {
            context.Result = new ObjectResult(new
            {
                message = $"Access denied. Role '{role.Name}' does not have '{AccessLevel}' permission for '{Feature}'.",
                requiredFeature = Feature,
                requiredAccessLevel = AccessLevel
            })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }
}
