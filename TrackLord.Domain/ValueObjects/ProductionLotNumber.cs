using System;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.ValueObjects;

public sealed record ProductionLotNumber
{
    public string Value { get; }

    public ProductionLotNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Production Lot Number cannot be empty or whitespace.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 50)
        {
            throw new DomainException("Production Lot Number cannot exceed 50 characters.");
        }

        Value = trimmed;
    }

    public static ProductionLotNumber Create(string value) => new(value);

    public override string ToString() => Value;

    public static implicit operator string(ProductionLotNumber lotNumber) => lotNumber.Value;
}
