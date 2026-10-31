using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;

namespace TrackLord.Api.Authorization;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class VaaniVoiceTokenAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    public int Order => int.MinValue;

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments.Values.OfType<IVaaniVoiceTokenRequest>().FirstOrDefault() is not { } request)
        {
            context.Result = new BadRequestObjectResult(new { message = "A voice session token is required." });
            return Task.CompletedTask;
        }

        try
        {
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                request.VoiceToken,
                VaaniVoiceAccessToken.CreateValidationParameters(configuration),
                out _);

            var tenantId = principal.FindFirstValue("tenant_id");
            if (principal.FindFirstValue(VaaniVoiceAccessToken.ScopeClaim) != VaaniVoiceAccessToken.Scope ||
                !Guid.TryParse(tenantId, out var parsedTenantId) ||
                parsedTenantId == Guid.Empty)
            {
                context.Result = new UnauthorizedObjectResult(new { message = "The voice session token is invalid." });
                return Task.CompletedTask;
            }

            context.HttpContext.User = principal;
            context.HttpContext.Request.Headers["X-Tenant-Id"] = parsedTenantId.ToString();
        }
        catch (SecurityTokenException)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "The voice session token is invalid or expired." });
            return Task.CompletedTask;
        }
        catch (ArgumentException)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "The voice session token is invalid or expired." });
            return Task.CompletedTask;
        }

        return next();
    }
}

public interface IVaaniVoiceTokenRequest
{
    string VoiceToken { get; }
}
