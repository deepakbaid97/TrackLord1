using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Infrastructure.Persistence;
using TrackLord.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TrackLord.Tests;

public class UserManagementTests
{
    [Fact]
    public async Task CreateUser_ShouldBelongToTenantAndBeRetrievable()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new MockTenantContext(tenantId);

        using (var db = new TrackLordDbContext(options, context))
        {
            var repo = new UserRepository(db);
            var newUser = new User(Guid.NewGuid(), "+919876543210", "Rajesh Kumar", "Operator", tenantId: tenantId);

            await repo.AddAsync(newUser);
        }

        using (var queryDb = new TrackLordDbContext(options, context))
        {
            var repo = new UserRepository(queryDb);
            var fetched = await repo.GetByPhoneNumberAsync("+919876543210");

            fetched.Should().NotBeNull();
            fetched!.Name.Should().Be("Rajesh Kumar");
            fetched.Role.Should().Be("Operator");
            fetched.TenantId.Should().Be(tenantId);
        }
    }

    [Fact]
    public async Task UpdateUserProfile_ShouldUpdateNameAndRole()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new MockTenantContext(tenantId);
        var userId = Guid.NewGuid();

        using (var db = new TrackLordDbContext(options, context))
        {
            var repo = new UserRepository(db);
            var newUser = new User(userId, "+919876543211", "Initial Name", "Operator", tenantId: tenantId);
            await repo.AddAsync(newUser);
        }

        using (var updateDb = new TrackLordDbContext(options, context))
        {
            var repo = new UserRepository(updateDb);
            var user = await repo.GetByIdAsync(userId);
            user.Should().NotBeNull();

            user!.UpdateProfile("Updated Name", "Supervisor");
            await repo.UpdateAsync(user);
        }

        using (var queryDb = new TrackLordDbContext(options, context))
        {
            var repo = new UserRepository(queryDb);
            var user = await repo.GetByIdAsync(userId);

            user.Should().NotBeNull();
            user!.Name.Should().Be("Updated Name");
            user.Role.Should().Be("Supervisor");
        }
    }

    [Fact]
    public async Task GetByPhoneNumberAsync_ShouldFindUserInSpecificTenant_EvenWhenUnauthenticated()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        // Seed user into specific tenant
        var adminUser = new User(Guid.NewGuid(), "+919876543210", "Apex Administrator", "Admin", tenantId: tenantId);
        using (var seedDb = new TrackLordDbContext(options, new MockTenantContext(tenantId)))
        {
            var repo = new UserRepository(seedDb);
            await repo.AddAsync(adminUser);
        }

        // Unauthenticated lookup (null/default tenant context)
        using (var unauthDb = new TrackLordDbContext(options, tenantContext: null))
        {
            var repo = new UserRepository(unauthDb);
            var foundUser = await repo.GetByPhoneNumberAsync("+919876543210");

            foundUser.Should().NotBeNull();
            foundUser!.TenantId.Should().Be(tenantId);
            foundUser.Name.Should().Be("Apex Administrator");
            foundUser.Role.Should().Be("Admin");
        }
    }
}
