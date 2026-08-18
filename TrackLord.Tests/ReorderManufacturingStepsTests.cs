using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TrackLord.Application.Commands;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TrackLord.Tests;

public class ReorderManufacturingStepsTests
{
    [Fact]
    public async Task ReorderManufacturingSteps_ShouldUpdateSequenceOrdersCorrectly()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var stepRepo = new ManufacturingStepRepository(db);

        var factoryId = Guid.NewGuid();
        var step1 = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var step2 = new ManufacturingStep(Guid.NewGuid(), "SEW", "Sewing", 2, factoryId);
        var step3 = new ManufacturingStep(Guid.NewGuid(), "PACK", "Packing", 3, factoryId);

        await stepRepo.AddAsync(step1);
        await stepRepo.AddAsync(step2);
        await stepRepo.AddAsync(step3);

        var handler = new ReorderManufacturingStepsCommandHandler(stepRepo);

        // Act - reorder: PACK first (1), CUT second (2), SEW third (3)
        var reorderCommand = new ReorderManufacturingStepsCommand(new List<StepOrderDto>
        {
            new(step3.Id, 1),
            new(step1.Id, 2),
            new(step2.Id, 3)
        });

        var result = await handler.Handle(reorderCommand, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        var allSteps = await stepRepo.GetAllAsync();
        allSteps.Should().HaveCount(3);

        allSteps[0].Code.Should().Be("PACK");
        allSteps[0].SequenceOrder.Should().Be(1);

        allSteps[1].Code.Should().Be("CUT");
        allSteps[1].SequenceOrder.Should().Be(2);

        allSteps[2].Code.Should().Be("SEW");
        allSteps[2].SequenceOrder.Should().Be(3);
    }
}
