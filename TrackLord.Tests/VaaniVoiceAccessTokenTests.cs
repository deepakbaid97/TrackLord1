using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TrackLord.Api.Authorization;
using TrackLord.Api.Controllers;

namespace TrackLord.Tests;

public class VaaniVoiceAccessTokenTests
{
    [Fact]
    public void Create_issuesShortLivedTokenWithUserAndReadOnlyScopeWithoutRoleClaims()
    {
        var configuration = CreateConfiguration();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var token = VaaniVoiceAccessToken.Create(
            configuration,
            userId.ToString(),
            "+15551234567",
            tenantId);
        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            VaaniVoiceAccessToken.CreateValidationParameters(configuration),
            out var validatedToken);

        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(userId.ToString());
        principal.FindFirstValue("tenant_id").Should().Be(tenantId.ToString());
        principal.FindFirstValue(VaaniVoiceAccessToken.ScopeClaim).Should().Be(VaaniVoiceAccessToken.Scope);
        principal.Claims.Should().NotContain(claim =>
            claim.Type == "role" || claim.Type == ClaimTypes.Role || claim.Type == "role_id");
        validatedToken.ValidTo.Should().BeCloseTo(
            DateTime.UtcNow.Add(VaaniVoiceAccessToken.Lifetime),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_issuesTokenRejectedByNormalTrackLordAudience()
    {
        var configuration = CreateConfiguration();
        var token = VaaniVoiceAccessToken.Create(
            configuration,
            Guid.NewGuid().ToString(),
            "+15551234567",
            Guid.NewGuid());
        var normalApiValidation = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(configuration["VaaniVoiceToken:SecretKey"]!)),
            ValidateIssuer = true,
            ValidIssuer = "TrackLordApi",
            ValidateAudience = true,
            ValidAudience = "TrackLordWeb",
            ValidateLifetime = true
        };

        var act = () => new JwtSecurityTokenHandler().ValidateToken(token, normalApiValidation, out _);

        act.Should().Throw<SecurityTokenInvalidAudienceException>();
    }

    [Fact]
    public void ValidateConfiguration_throwsWhenVoiceAndApiSigningKeysMatch()
    {
        var sharedKey = "test-signing-key-with-at-least-32-characters";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VaaniVoiceToken:SecretKey"] = sharedKey
            })
            .Build();

        var act = () => VaaniVoiceAccessToken.ValidateConfiguration(configuration, sharedKey);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be different*");
    }

    [Fact]
    public void Create_throwsWhenVoiceSigningKeyIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "test-signing-key-with-at-least-32-characters"
            })
            .Build();

        var act = () => VaaniVoiceAccessToken.Create(
            configuration,
            Guid.NewGuid().ToString(),
            "+15551234567",
            Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*VaaniVoiceToken:SecretKey*at least 32 bytes*");
    }

    [Fact]
    public async Task VoiceTokenFilter_usesTokenTenantInsteadOfCallerSuppliedTenantHeader()
    {
        var configuration = CreateConfiguration();
        var tenantId = Guid.NewGuid();
        var token = VaaniVoiceAccessToken.Create(
            configuration,
            Guid.NewGuid().ToString(),
            "+15551234567",
            tenantId);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .BuildServiceProvider()
        };
        httpContext.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>
            {
                ["request"] = new VaaniVoiceToolRequest(token)
            },
            new object());
        var nextExecuted = false;

        await new VaaniVoiceTokenAttribute().OnActionExecutionAsync(context, () =>
        {
            nextExecuted = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        nextExecuted.Should().BeTrue();
        context.HttpContext.Request.Headers["X-Tenant-Id"].ToString().Should().Be(tenantId.ToString());
        context.HttpContext.User.FindFirstValue("tenant_id").Should().Be(tenantId.ToString());
        context.HttpContext.User.Claims.Should().NotContain(claim =>
            claim.Type == "role" || claim.Type == ClaimTypes.Role || claim.Type == "role_id");
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "test-signing-key-with-at-least-32-characters",
                ["VaaniVoiceToken:SecretKey"] = "test-voice-signing-key-with-at-least-32-characters",
                ["JwtSettings:Issuer"] = "TrackLordApi"
            })
            .Build();
    }
}
