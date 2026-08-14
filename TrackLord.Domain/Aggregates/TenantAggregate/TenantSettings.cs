using System.Collections.Generic;

namespace TrackLord.Domain.Aggregates.TenantAggregate;

public enum ClientTrackingMode
{
    LotLevel = 1,     // Client assigned once per Production Lot
    StepLevel = 2,    // Client assigned per Step Movement (Job-work)
    None = 3          // No client tracking
}

public record TenantSettings
{
    public ClientTrackingMode ClientMode { get; init; } = ClientTrackingMode.StepLevel;
    public Dictionary<string, bool> FeatureOverrides { get; init; } = new();

    public bool IsFeatureEnabled(string key, bool defaultValue = true)
    {
        if (FeatureOverrides != null && FeatureOverrides.TryGetValue(key, out var val))
        {
            return val;
        }
        return defaultValue;
    }
}
