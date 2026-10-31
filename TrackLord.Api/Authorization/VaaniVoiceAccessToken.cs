using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TrackLord.Api.Authorization;

public static class VaaniVoiceAccessToken
{
    public const string Audience = "TrackLordVaaniReadOnlyTools";
    public const string ScopeClaim = "voice_scope";
    public const string Scope = "read_only";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(20);

    public static string Create(
        IConfiguration configuration,
        string userId,
        string phoneNumber,
        Guid tenantId)
    {
        var (key, issuer) = GetSigningDetails(configuration);
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.MobilePhone, phoneNumber),
            new Claim("phone_number", phoneNumber),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ScopeClaim, Scope)
        };
        var token = new JwtSecurityToken(
            issuer,
            Audience,
            claims,
            now,
            now.Add(Lifetime),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static TokenValidationParameters CreateValidationParameters(IConfiguration configuration)
    {
        var (key, issuer) = GetSigningDetails(configuration);
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero
        };
    }

    private static (SymmetricSecurityKey Key, string Issuer) GetSigningDetails(IConfiguration configuration)
    {
        var jwtSecretKey = SigningKeyConfiguration.GetRequiredSecret(
            configuration,
            "JwtSettings:SecretKey");
        var secretKey = SigningKeyConfiguration.GetRequiredSecret(
            configuration,
            "VaaniVoiceToken:SecretKey");
        EnsureSigningKeysAreDistinct(secretKey, jwtSecretKey);
        var issuer = configuration["JwtSettings:Issuer"] ?? "TrackLordApi";
        return (new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)), issuer);
    }

    public static void ValidateConfiguration(IConfiguration configuration, string jwtSecretKey)
    {
        var voiceSecretKey = SigningKeyConfiguration.GetRequiredSecret(
            configuration,
            "VaaniVoiceToken:SecretKey");
        EnsureSigningKeysAreDistinct(voiceSecretKey, jwtSecretKey);
    }

    private static void EnsureSigningKeysAreDistinct(string voiceSecretKey, string jwtSecretKey)
    {
        if (string.Equals(voiceSecretKey, jwtSecretKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "VaaniVoiceToken:SecretKey must be different from JwtSettings:SecretKey.");
        }
    }
}
