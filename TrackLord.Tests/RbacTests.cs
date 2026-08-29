using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TrackLord.Application.Commands;
using TrackLord.Application.Queries;
using TrackLord.Domain.Aggregates.RoleAggregate;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.ValueObjects;
using TrackLord.Infrastructure.Persistence;
using TrackLord.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TrackLord.Tests;

public class RbacTests
{
    [Fact]
    public void AdminRole_ShouldAlwaysHaveAllPermissions()
    {
        var tenantId = Guid.NewGuid();
        var adminRole = new Role(
            Guid.NewGuid(),
            "Admin",
            "Full admin access",
            isSystemRole: true,
            permissionsJson: "{}",
            tenantId: tenantId
        );

        adminRole.HasPermission(PermissionFeatures.ProductionLots, AccessLevels.Read).Should().BeTrue();
        adminRole.HasPermission(PermissionFeatures.ProductionLots, AccessLevels.Delete).Should().BeTrue();
        adminRole.HasPermission(PermissionFeatures.Movements, AccessLevels.Write).Should().BeTrue();
        adminRole.HasPermission(PermissionFeatures.MovementReversals, AccessLevels.Write).Should().BeTrue();
        adminRole.HasPermission(PermissionFeatures.MaterialLots, AccessLevels.Write).Should().BeTrue();
        adminRole.HasPermission(PermissionFeatures.Roles, AccessLevels.Write).Should().BeTrue();
        adminRole.HasPermission(PermissionFeatures.TenantSettings, AccessLevels.Write).Should().BeTrue();

        var dict = adminRole.GetPermissionsDictionary();
        dict.Should().ContainKey(PermissionFeatures.ProductionLots);
        dict.Should().ContainKey(PermissionFeatures.Roles);
        dict[PermissionFeatures.ProductionLots].Should().Contain(AccessLevels.Delete);
    }

    [Fact]
    public void FloorInchargeRole_ShouldHaveMaterialAndReversalPermissions_ButNotStepMovementWrite()
    {
        var tenantId = Guid.NewGuid();
        var permissions = new Dictionary<string, List<string>>
        {
            [PermissionFeatures.MaterialLots] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.MaterialConsumptions] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.MovementReversals] = new() { AccessLevels.Write },
            [PermissionFeatures.ProductionLots] = new() { AccessLevels.Read },
            [PermissionFeatures.Movements] = new() { AccessLevels.Read },
            [PermissionFeatures.Dashboard] = new() { AccessLevels.Read }
        };

        var floorIncharge = new Role(
            Guid.NewGuid(),
            "Floor Incharge",
            "Floor oversight: materials, consumptions, and movement reversals",
            isSystemRole: false,
            permissionsJson: JsonSerializer.Serialize(permissions),
            tenantId: tenantId
        );

        // Material permissions
        floorIncharge.HasPermission(PermissionFeatures.MaterialLots, AccessLevels.Read).Should().BeTrue();
        floorIncharge.HasPermission(PermissionFeatures.MaterialLots, AccessLevels.Write).Should().BeTrue();
        floorIncharge.HasPermission(PermissionFeatures.MaterialLots, AccessLevels.Delete).Should().BeFalse();
        floorIncharge.HasPermission(PermissionFeatures.MaterialConsumptions, AccessLevels.Write).Should().BeTrue();

        // Movement Reversals
        floorIncharge.HasPermission(PermissionFeatures.MovementReversals, AccessLevels.Write).Should().BeTrue();

        // Step Movements (Read only, cannot write/move)
        floorIncharge.HasPermission(PermissionFeatures.Movements, AccessLevels.Read).Should().BeTrue();
        floorIncharge.HasPermission(PermissionFeatures.Movements, AccessLevels.Write).Should().BeFalse();

        // Admin features
        floorIncharge.HasPermission(PermissionFeatures.Roles, AccessLevels.Write).Should().BeFalse();
        floorIncharge.HasPermission(PermissionFeatures.TenantSettings, AccessLevels.Write).Should().BeFalse();
    }

    [Fact]
    public void JobWorkerRole_ShouldHaveStepMovementWrite_ButNotMaterialOrReversalPermissions()
    {
        var tenantId = Guid.NewGuid();
        var permissions = new Dictionary<string, List<string>>
        {
            [PermissionFeatures.Movements] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.ProductionLots] = new() { AccessLevels.Read },
            [PermissionFeatures.Dashboard] = new() { AccessLevels.Read }
        };

        var jobWorker = new Role(
            Guid.NewGuid(),
            "Job Worker",
            "Can transfer lot quantities across steps",
            isSystemRole: false,
            permissionsJson: JsonSerializer.Serialize(permissions),
            tenantId: tenantId
        );

        // Movement permissions
        jobWorker.HasPermission(PermissionFeatures.Movements, AccessLevels.Read).Should().BeTrue();
        jobWorker.HasPermission(PermissionFeatures.Movements, AccessLevels.Write).Should().BeTrue();

        // Must NOT have reversals or material permissions
        jobWorker.HasPermission(PermissionFeatures.MovementReversals, AccessLevels.Write).Should().BeFalse();
        jobWorker.HasPermission(PermissionFeatures.MaterialLots, AccessLevels.Write).Should().BeFalse();
        jobWorker.HasPermission(PermissionFeatures.MaterialLots, AccessLevels.Read).Should().BeFalse();
        jobWorker.HasPermission(PermissionFeatures.Roles, AccessLevels.Read).Should().BeFalse();
    }

    [Fact]
    public void QcInspectorRole_ShouldHaveRejectionsWrite_ButNotMovementsWrite()
    {
        var tenantId = Guid.NewGuid();
        var permissions = new Dictionary<string, List<string>>
        {
            [PermissionFeatures.RejectionRecords] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.ProductionLots] = new() { AccessLevels.Read },
            [PermissionFeatures.Movements] = new() { AccessLevels.Read },
            [PermissionFeatures.MaterialConsumptions] = new() { AccessLevels.Read },
            [PermissionFeatures.Dashboard] = new() { AccessLevels.Read }
        };

        var qcRole = new Role(
            Guid.NewGuid(),
            "QC Inspector",
            "Quality control and defect recording",
            isSystemRole: false,
            permissionsJson: JsonSerializer.Serialize(permissions),
            tenantId: tenantId
        );

        qcRole.HasPermission(PermissionFeatures.RejectionRecords, AccessLevels.Write).Should().BeTrue();
        qcRole.HasPermission(PermissionFeatures.ProductionLots, AccessLevels.Read).Should().BeTrue();
        qcRole.HasPermission(PermissionFeatures.Movements, AccessLevels.Write).Should().BeFalse();
        qcRole.HasPermission(PermissionFeatures.MaterialLots, AccessLevels.Write).Should().BeFalse();
    }

    [Fact]
    public void SystemRole_Admin_CannotBeRenamedOrDeactivated()
    {
        var admin = new Role(Guid.NewGuid(), "Admin", isSystemRole: true);

        var renameAct = () => admin.UpdateDetails("SuperUser", "Renamed", true);
        renameAct.Should().Throw<DomainException>().WithMessage("*cannot be renamed*");

        var deactivateAct = () => admin.Deactivate();
        deactivateAct.Should().Throw<DomainException>().WithMessage("*cannot be deactivated*");
    }

    [Fact]
    public async Task CreateRoleCommand_ShouldPersistRole_AndPreventDuplicateName()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new MockTenantContext(tenantId);

        using (var db = new TrackLordDbContext(options, context))
        {
            var roleRepo = new RoleRepository(db);
            var handler = new CreateRoleCommandHandler(roleRepo);

            var permissionsJson = "{\"material_lots\":[\"read\",\"write\"]}";
            var command = new CreateRoleCommand("Store Keeper", "Manages raw material inventory", permissionsJson);

            var roleId = await handler.Handle(command, CancellationToken.None);
            roleId.Should().NotBeEmpty();

            // Attempting to create duplicate role name in same tenant should throw
            var duplicateCommand = new CreateRoleCommand("Store Keeper", "Duplicate", permissionsJson);
            var duplicateAct = () => handler.Handle(duplicateCommand, CancellationToken.None);
            await duplicateAct.Should().ThrowAsync<DomainException>().WithMessage("*already exists*");
        }

        using (var queryDb = new TrackLordDbContext(options, context))
        {
            var roleRepo = new RoleRepository(queryDb);
            var fetched = await roleRepo.GetByNameAsync("Store Keeper");

            fetched.Should().NotBeNull();
            fetched!.Name.Should().Be("Store Keeper");
            fetched.Description.Should().Be("Manages raw material inventory");
            fetched.HasPermission(PermissionFeatures.MaterialLots, AccessLevels.Write).Should().BeTrue();
        }
    }

    [Fact]
    public async Task UpdateRoleCommand_ShouldUpdateNameAndPermissions()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new MockTenantContext(tenantId);
        var roleId = Guid.NewGuid();

        using (var db = new TrackLordDbContext(options, context))
        {
            var roleRepo = new RoleRepository(db);
            var role = new Role(roleId, "Helper", "Initial helper role", permissionsJson: "{\"movements\":[\"read\"]}", tenantId: tenantId);
            await roleRepo.AddAsync(role);
        }

        using (var updateDb = new TrackLordDbContext(options, context))
        {
            var roleRepo = new RoleRepository(updateDb);
            var handler = new UpdateRoleCommandHandler(roleRepo);

            var updatedPermissions = "{\"movements\":[\"read\",\"write\"],\"production_lots\":[\"read\"]}";
            var command = new UpdateRoleCommand(roleId, "Senior Helper", "Promoted helper role", updatedPermissions, IsActive: true);

            await handler.Handle(command, CancellationToken.None);
        }

        using (var queryDb = new TrackLordDbContext(options, context))
        {
            var roleRepo = new RoleRepository(queryDb);
            var fetched = await roleRepo.GetByIdAsync(roleId);

            fetched.Should().NotBeNull();
            fetched!.Name.Should().Be("Senior Helper");
            fetched.Description.Should().Be("Promoted helper role");
            fetched.HasPermission(PermissionFeatures.Movements, AccessLevels.Write).Should().BeTrue();
            fetched.HasPermission(PermissionFeatures.ProductionLots, AccessLevels.Read).Should().BeTrue();
        }
    }

    [Fact]
    public async Task DeleteRoleCommand_ShouldPreventDeletingSystemRoleOrRoleWithAssignedUsers()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new MockTenantContext(tenantId);
        var systemRoleId = Guid.NewGuid();
        var customRoleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var db = new TrackLordDbContext(options, context))
        {
            var roleRepo = new RoleRepository(db);
            var userRepo = new UserRepository(db);

            var systemRole = new Role(systemRoleId, "Admin", isSystemRole: true, tenantId: tenantId);
            var customRole = new Role(customRoleId, "Custom Lead", isSystemRole: false, tenantId: tenantId);
            await roleRepo.AddRangeAsync(new[] { systemRole, customRole });

            var user = new User(userId, "+919876543299", "Assigned Operator", "Custom Lead", tenantId: tenantId, roleId: customRoleId);
            await userRepo.AddAsync(user);
        }

        using (var deleteDb = new TrackLordDbContext(options, context))
        {
            var roleRepo = new RoleRepository(deleteDb);
            var handler = new DeleteRoleCommandHandler(roleRepo);

            // 1. Cannot delete system role
            var systemDeleteAct = () => handler.Handle(new DeleteRoleCommand(systemRoleId), CancellationToken.None);
            await systemDeleteAct.Should().ThrowAsync<DomainException>().WithMessage("*System role*cannot be deleted*");

            // 2. Cannot delete custom role with assigned users
            var assignedDeleteAct = () => handler.Handle(new DeleteRoleCommand(customRoleId), CancellationToken.None);
            await assignedDeleteAct.Should().ThrowAsync<DomainException>().WithMessage("*team members are currently assigned*");
        }
    }

    [Fact]
    public async Task AssignRoleToUserCommand_ShouldAssignRoleAndKeepSynced()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new MockTenantContext(tenantId);
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var db = new TrackLordDbContext(options, context))
        {
            var roleRepo = new RoleRepository(db);
            var userRepo = new UserRepository(db);

            var newRole = new Role(roleId, "Floor Incharge", "Floor leadership", tenantId: tenantId);
            var user = new User(userId, "+919876543200", "Worker One", "Operator", tenantId: tenantId);

            await roleRepo.AddAsync(newRole);
            await userRepo.AddAsync(user);
        }

        using (var assignDb = new TrackLordDbContext(options, context))
        {
            var roleRepo = new RoleRepository(assignDb);
            var userRepo = new UserRepository(assignDb);
            var handler = new AssignRoleToUserCommandHandler(userRepo, roleRepo);

            await handler.Handle(new AssignRoleToUserCommand(userId, roleId), CancellationToken.None);
        }

        using (var queryDb = new TrackLordDbContext(options, context))
        {
            var userRepo = new UserRepository(queryDb);
            var updatedUser = await userRepo.GetByIdAsync(userId);

            updatedUser.Should().NotBeNull();
            updatedUser!.RoleId.Should().Be(roleId);
            updatedUser.Role.Should().Be("Floor Incharge");
        }
    }

    [Fact]
    public async Task RoleQueries_ShouldReturnCatalogAndRoleDtos()
    {
        var catalogHandler = new GetPermissionCatalogQueryHandler();
        var catalog = await catalogHandler.Handle(new GetPermissionCatalogQuery(), CancellationToken.None);

        catalog.Should().NotBeEmpty();
        catalog.Should().Contain(p => p.Feature == PermissionFeatures.ProductionLots);
        catalog.Should().Contain(p => p.Feature == PermissionFeatures.Movements);
        catalog.Should().Contain(p => p.Feature == PermissionFeatures.MovementReversals);
        catalog.Should().Contain(p => p.Feature == PermissionFeatures.MaterialLots);
        catalog.Should().Contain(p => p.Feature == PermissionFeatures.MaterialConsumptions);
        catalog.Should().Contain(p => p.Feature == PermissionFeatures.RejectionRecords);
        catalog.Should().Contain(p => p.Feature == PermissionFeatures.Roles);
    }
}
