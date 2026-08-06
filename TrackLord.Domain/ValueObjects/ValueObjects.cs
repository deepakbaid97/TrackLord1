using System;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.ValueObjects;

public sealed record MaterialLotNumber
{
    public string Value { get; }

    public MaterialLotNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Material Lot Number cannot be empty or whitespace.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 50)
        {
            throw new DomainException("Material Lot Number cannot exceed 50 characters.");
        }

        Value = trimmed;
    }

    public static MaterialLotNumber Create(string value) => new(value);

    public override string ToString() => Value;

    public static implicit operator string(MaterialLotNumber lotNumber) => lotNumber.Value;
}

public sealed record Quantity
{
    public decimal Value { get; }
    public UnitType Unit { get; }

    public Quantity(decimal value, UnitType unit)
    {
        if (value < 0)
        {
            throw new DomainException($"Quantity cannot be negative. Value provided: {value}");
        }

        Value = value;
        Unit = unit;
    }

    public static Quantity Zero(UnitType unit) => new(0, unit);

    public Quantity Add(Quantity other)
    {
        if (Unit != other.Unit)
        {
            throw new DomainException($"Cannot add quantities with different units: {Unit} and {other.Unit}");
        }

        return new Quantity(Value + other.Value, Unit);
    }

    public Quantity Subtract(Quantity other)
    {
        if (Unit != other.Unit)
        {
            throw new DomainException($"Cannot subtract quantities with different units: {Unit} and {other.Unit}");
        }

        if (Value < other.Value)
        {
            throw new DomainException($"Insufficient quantity to subtract. Current: {Value}, Attempted: {other.Value}");
        }

        return new Quantity(Value - other.Value, Unit);
    }

    public override string ToString() => $"{Value} {Unit}";
}
