using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackLord.Api.Authorization;
using TrackLord.Domain.Aggregates.RoleAggregate;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.ValueObjects;
using TrackLord.Infrastructure.Persistence;
using TrackLord.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TrackLord.Tests;

public class PermissionFilterTests
{
    private (ActionExecutingContext context, bool executed) CreateFilterContext(
        ClaimsPrincipal principal,
        IServiceProvider services)
    {
        var httpContext = new DefaultHttpContext
        {
            User = principal,
            RequestServices = services
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object()
        );

        return (context, false);
    }

    [Fact]
    public async Task RequirePermissionFilter_AdminUserClaim_ShouldPassImmediately()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var services = new ServiceCollection().BuildServiceProvider();
        var (context, _) = CreateFilterContext(principal, services);

        var filter = new RequirePermissionAttribute(PermissionFeatures.ProductionLots, AccessLevels.Delete);
        var nextExecuted = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextExecuted = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        nextExecuted.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task RequirePermissionFilter_FloorIncharge_ShouldAllowMaterialsAndBlockMovements()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var floorPermissions = new Dictionary<string, List<string>>
        {
            [PermissionFeatures.MaterialLots] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.MaterialConsumptions] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.MovementReversals] = new() { AccessLevels.Write }
        };

        var role = new Role(roleId, "Floor Incharge", permissionsJson: JsonSerializer.Serialize(floorPermissions), tenantId: tenantId);
        var user = new User(userId, "+919876543210", "Floor Lead", "Floor Incharge", tenantId: tenantId, roleId: roleId);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<TrackLordDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        serviceCollection.AddScoped<ITenantContext>(_ => new MockTenantContext(tenantId));
        serviceCollection.AddScoped<IUserRepository, UserRepository>();
        serviceCollection.AddScoped<IRoleRepository, RoleRepository>();
        var services = serviceCollection.BuildServiceProvider();

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackLordDbContext>();
            db.Roles.Add(role);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("phone_number", user.PhoneNumber),
            new("role", "Floor Incharge"),
            new("role_id", roleId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // 1. Check access to Material Lots (Write) -> Allowed
        using (var scope = services.CreateScope())
        {
            var (context, _) = CreateFilterContext(principal, scope.ServiceProvider);
            var filter = new RequirePermissionAttribute(PermissionFeatures.MaterialLots, AccessLevels.Write);
            var nextExecuted = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextExecuted = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
            });

            nextExecuted.Should().BeTrue();
            context.Result.Should().BeNull();
        }

        // 2. Check access to Movement Reversals (Write) -> Allowed
        using (var scope = services.CreateScope())
        {
            var (context, _) = CreateFilterContext(principal, scope.ServiceProvider);
            var filter = new RequirePermissionAttribute(PermissionFeatures.MovementReversals, AccessLevels.Write);
            var nextExecuted = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextExecuted = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
            });

            nextExecuted.Should().BeTrue();
            context.Result.Should().BeNull();
        }

        // 3. Check access to Step Movements (Write) -> Blocked (403)
        using (var scope = services.CreateScope())
        {
            var (context, _) = CreateFilterContext(principal, scope.ServiceProvider);
            var filter = new RequirePermissionAttribute(PermissionFeatures.Movements, AccessLevels.Write);
            var nextExecuted = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextExecuted = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
            });

            nextExecuted.Should().BeFalse();
            context.Result.Should().BeOfType<ObjectResult>();
            (context.Result as ObjectResult)!.StatusCode.Should().Be(403);
        }
    }

    [Fact]
    public async Task RequirePermissionFilter_JobWorker_ShouldAllowMovementsAndBlockMaterials()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var jobWorkerPermissions = new Dictionary<string, List<string>>
        {
            [PermissionFeatures.Movements] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.ProductionLots] = new() { AccessLevels.Read }
        };

        var role = new Role(roleId, "Job Worker", permissionsJson: JsonSerializer.Serialize(jobWorkerPermissions), tenantId: tenantId);
        var user = new User(userId, "+919876543211", "Worker Bob", "Job Worker", tenantId: tenantId, roleId: roleId);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<TrackLordDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        serviceCollection.AddScoped<ITenantContext>(_ => new MockTenantContext(tenantId));
        serviceCollection.AddScoped<IUserRepository, UserRepository>();
        serviceCollection.AddScoped<IRoleRepository, RoleRepository>();
        var services = serviceCollection.BuildServiceProvider();

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackLordDbContext>();
            db.Roles.Add(role);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("phone_number", user.PhoneNumber),
            new("role", "Job Worker"),
            new("role_id", roleId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // 1. Move lot (Movements: Write) -> Allowed
        using (var scope = services.CreateScope())
        {
            var (context, _) = CreateFilterContext(principal, scope.ServiceProvider);
            var filter = new RequirePermissionAttribute(PermissionFeatures.Movements, AccessLevels.Write);
            var nextExecuted = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextExecuted = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
            });

            nextExecuted.Should().BeTrue();
            context.Result.Should().BeNull();
        }

        // 2. Material consumption or material lot -> Blocked (403)
        using (var scope = services.CreateScope())
        {
            var (context, _) = CreateFilterContext(principal, scope.ServiceProvider);
            var filter = new RequirePermissionAttribute(PermissionFeatures.MaterialLots, AccessLevels.Write);
            var nextExecuted = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextExecuted = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
            });

            nextExecuted.Should().BeFalse();
            context.Result.Should().BeOfType<ObjectResult>();
            (context.Result as ObjectResult)!.StatusCode.Should().Be(403);
        }

        // 3. Movement reversal -> Blocked (403)
        using (var scope = services.CreateScope())
        {
            var (context, _) = CreateFilterContext(principal, scope.ServiceProvider);
            var filter = new RequirePermissionAttribute(PermissionFeatures.MovementReversals, AccessLevels.Write);
            var nextExecuted = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextExecuted = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
            });

            nextExecuted.Should().BeFalse();
            context.Result.Should().BeOfType<ObjectResult>();
            (context.Result as ObjectResult)!.StatusCode.Should().Be(403);
        }
    }
}
