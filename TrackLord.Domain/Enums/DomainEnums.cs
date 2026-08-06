namespace TrackLord.Domain.Enums;

public enum LotStatus
{
    Active = 1,
    Completed = 2,
    Cancelled = 3,
    Archived = 4
}

public enum MovementType
{
    InitialProduction = 1,
    StepTransfer = 2,
    Rejection = 3,
    Wastage = 4,
    Adjustment = 5,
    Reversal = 6
}

public enum UnitType
{
    Pieces = 1,
    Meters = 2,
    Kilograms = 3,
    Dozens = 4,
    Yards = 5,
    Grams = 6
}
