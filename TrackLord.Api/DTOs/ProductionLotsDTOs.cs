namespace TrackLord.Api.DTOs;

public record RejectItemsRequest(
    Guid StepId,
    Guid? TargetStepId,
    decimal QuantityRejected,
    string Reason,
    DateTime RejectionDate,
    Guid? ClientId,
    string? CreatedBy);