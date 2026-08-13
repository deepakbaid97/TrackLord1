namespace TrackLord.Api.DTOs;

public record UpdateManufacturingStepRequest(string Name, int SequenceOrder, bool IsTerminating, bool IsActive);
