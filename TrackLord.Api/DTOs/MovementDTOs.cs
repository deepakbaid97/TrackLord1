namespace TrackLord.Api.DTOs;

public record EditMovementRequest(Guid? FromStepId, Guid ToStepId, decimal NewQuantity, DateTime NewMovementDate, string? Remarks, string? ReferenceNumber);
public record ReverseMovementRequest(string Reason, string? User);
