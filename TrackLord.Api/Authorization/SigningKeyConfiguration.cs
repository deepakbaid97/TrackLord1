using System.Text;

namespace TrackLord.Api.Authorization;

internal static class SigningKeyConfiguration
{
    private const int MinimumKeyLengthBytes = 32;

    public static string GetRequiredSecret(IConfiguration configuration, string settingName)
    {
        var secret = configuration[settingName];
        if (string.IsNullOrWhiteSpace(secret) ||
            Encoding.UTF8.GetByteCount(secret) < MinimumKeyLengthBytes)
        {
            throw new InvalidOperationException(
                $"Configuration setting '{settingName}' must contain at least 32 bytes. " +
                "Set it with .NET User Secrets for development or an environment variable; do not commit signing keys.");
        }

        return secret;
    }
}
