using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TrackLord.Domain.Aggregates.TenantAggregate;

namespace TrackLord.Infrastructure.Services;

public record AuthTokenResult(string Token, DateTime ExpiresAt, string UserId, string PhoneNumber, Guid TenantId, string? Role = null, Guid? RoleId = null);

public interface IJwtTokenService
{
    AuthTokenResult GenerateToken(string userId, string phoneNumber, Guid? tenantId = null, string? role = null, Guid? roleId = null);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AuthTokenResult GenerateToken(string userId, string phoneNumber, Guid? tenantId = null, string? role = null, Guid? roleId = null)
    {
        var activeTenantId = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : Tenant.DefaultTenantId;
        var secretKey = _configuration["JwtSettings:SecretKey"] ?? "TrackLordSuperSecretAuthenticationKey2026!#ForJwtTokens";
        var issuer = _configuration["JwtSettings:Issuer"] ?? "TrackLordApi";
        var audience = _configuration["JwtSettings:Audience"] ?? "TrackLordWeb";
        var expiryMinutes = int.TryParse(_configuration["JwtSettings:ExpiryMinutes"], out var min) ? min : 480;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("phone_number", phoneNumber),
            new(ClaimTypes.MobilePhone, phoneNumber),
            new(ClaimTypes.NameIdentifier, userId),
            new("tenant_id", activeTenantId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim("role", role));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (roleId.HasValue && roleId.Value != Guid.Empty)
        {
            claims.Add(new Claim("role_id", roleId.Value.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return new AuthTokenResult(tokenString, expiresAt, userId, phoneNumber, activeTenantId, role, roleId);
    }
}
