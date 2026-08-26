namespace TrackLord.Api.DTOs;

public record RejectItemsRequest(
    Guid StepId,
    Guid? TargetStepId,
    decimal QuantityRejected,
    string Reason,
    DateTime RejectionDate,
    Guid? ClientId,
    string? CreatedBy);

public class BatchLotJourneysRequest
{
    public List<Guid>? LotIds { get; set; } = new();
}