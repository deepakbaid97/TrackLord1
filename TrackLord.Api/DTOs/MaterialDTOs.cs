namespace TrackLord.Api.DTOs;

public record EditMaterialConsumptionRequest(decimal QuantityConsumed, string? Remarks);
public record UpdateMaterialLotRequest(string LotNumber, decimal InitialQuantity, TrackLord.Domain.Enums.UnitType Unit, string? MaterialName = null, decimal UnitRate = 0m);

