using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Infrastructure.Persistence.Seed;

namespace SuperApp.Tests.Seed;

/// <summary>The seed fills a fresh database (ERD §12) and running it again adds nothing.</summary>
[Trait("Category", "Database")]
public sealed class ReferenceSeedTests(SeedDatabaseFixture fixture) : IClassFixture<SeedDatabaseFixture>
{
    private async Task RunSeedAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().RunAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task Seed_fills_reference_data_and_is_idempotent()
    {
        await RunSeedAsync();
        var first = await SnapshotAsync();
        await RunSeedAsync();
        (await SnapshotAsync()).ShouldBe(first);

        first["governorates"].ShouldBe(27);
        first["areas"].ShouldBe(12);
        first["permissions"].ShouldBe(Permissions.All.Count);
        first["roles"].ShouldBe(13);
        first["settings"].ShouldBeGreaterThan(20);
        first["flags"].ShouldBe(12);
        first["integrations"].ShouldBe(20);
        first["facilities"].ShouldBe(14);
        first["policies"].ShouldBe(4);
        first["shopCategories"].ShouldBe(9);
        first["designCategories"].ShouldBe(10);
        first["levels"].ShouldBe(4);
        first["departments"].ShouldBe(8);
    }

    [Fact]
    public async Task Ceo_gets_every_permission_and_holds_the_ceo_position()
    {
        await RunSeedAsync();
        var ceo = await fixture.QueryAsync(db => db.Users.SingleAsync(u => u.Email == SeedDatabaseFixture.CeoEmail));
        var ceoPermissions = await fixture.QueryAsync(db => db.Set<RolePermission>().CountAsync(rp => rp.Role.Name == SystemRoles.Ceo));
        var position = await fixture.QueryAsync(db => db.Set<OrgPosition>().SingleAsync(p => p.HolderUserId == ceo.Id));

        ceo.EmailConfirmed.ShouldBeTrue();
        ceoPermissions.ShouldBe(Permissions.All.Count);
        position.DepartmentId.ShouldBeNull();
    }

    [Fact]
    public async Task Marketing_cannot_read_revenue_and_only_primary_providers_are_marked()
    {
        await RunSeedAsync();
        var marketing = await fixture.QueryAsync(db => db.Set<RolePermission>()
            .Where(rp => rp.Role.Name == SystemRoles.Marketing).Select(rp => rp.Permission.Code).ToListAsync());
        var primaries = await fixture.QueryAsync(db => db.Set<Integration>().Where(i => i.IsPrimary)
            .GroupBy(i => i.Category).Select(g => g.Count()).ToListAsync());

        marketing.ShouldNotContain("revenue.read");
        marketing.ShouldContain("codes.manage");
        primaries.ShouldAllBe(n => n == 1);
    }

    private async Task<Dictionary<string, int>> SnapshotAsync() => new()
    {
        ["governorates"] = await fixture.CountAsync<Governorate>(),
        ["areas"] = await fixture.CountAsync<Area>(),
        ["permissions"] = await fixture.CountAsync<Permission>(),
        ["rolePermissions"] = await fixture.CountAsync<RolePermission>(),
        ["roles"] = await fixture.CountAsync<Role>(),
        ["users"] = await fixture.CountAsync<User>(),
        ["settings"] = await fixture.CountAsync<Setting>(),
        ["flags"] = await fixture.CountAsync<FeatureFlag>(),
        ["integrations"] = await fixture.CountAsync<Integration>(),
        ["facilities"] = await fixture.CountAsync<Facility>(),
        ["policies"] = await fixture.CountAsync<CancellationPolicy>(),
        ["shopCategories"] = await fixture.CountAsync<ProductCategory>(),
        ["designCategories"] = await fixture.CountAsync<DesignCategory>(),
        ["levels"] = await fixture.CountAsync<SellerLevel>(),
        ["departments"] = await fixture.CountAsync<Department>(),
        ["positions"] = await fixture.CountAsync<OrgPosition>(),
    };
}
