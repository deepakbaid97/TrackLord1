using System;
using System.Collections.Generic;
using TrackLord.Application.Commands;

namespace TrackLord.Api.DTOs;

public record UpdateManufacturingStepRequest(string Name, int SequenceOrder, bool IsTerminating, bool IsActive);
public record ReorderManufacturingStepsRequest(IReadOnlyList<StepOrderDto> Steps);
