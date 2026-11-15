using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence.Seed;

namespace SuperApp.Tests.Seed;

/// <summary>The demo data (ERD §12 step 11) matches today's site and is added only once.</summary>
[Trait("Category", "Database")]
public sealed class DemoSeedTests(SeedDatabaseFixture fixture) : IClassFixture<SeedDatabaseFixture>
{
    private async Task RunSeedAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().RunAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task Demo_data_matches_the_site_and_is_idempotent()
    {
        await RunSeedAsync();
        var first = await SnapshotAsync();
        await RunSeedAsync();
        (await SnapshotAsync()).ShouldBe(first);

        first["users"].ShouldBe(103);           // 102 demo people + the CEO from .env
        first["partners"].ShouldBe(4);
        first["pitches"].ShouldBe(12);
        first["openingHours"].ShouldBe(12 * 7);
        first["stores"].ShouldBe(5);
        first["products"].ShouldBe(35);
        first["sellers"].ShouldBe(3);
        first["designs"].ShouldBe(11);
        first["orders"].ShouldBe(52);
        first["orderItems"].ShouldBe(54);
        first["tournaments"].ShouldBe(3);
        first["posts"].ShouldBe(18);
        first["agents"].ShouldBe(18);
        first["positions"].ShouldBe(22);
    }

    [Fact]
    public async Task Every_agent_is_paused_has_permissions_and_holds_its_position()
    {
        await RunSeedAsync();
        var agents = await fixture.QueryAsync(db => db.Set<Agent>().Include(a => a.Permissions).Include(a => a.Position).ToListAsync());

        agents.ShouldAllBe(a => a.Status == AgentStatus.Paused);
        agents.ShouldAllBe(a => a.Permissions.Count > 0);
        agents.ShouldAllBe(a => a.Position != null && a.Position.HolderUserId == a.ServiceUserId);
        (await fixture.CountAsync<ApiKey>()).ShouldBe(0);
    }

    private async Task<Dictionary<string, int>> SnapshotAsync() => new()
    {
        ["users"] = await fixture.CountAsync<User>(),
        ["partners"] = await fixture.CountAsync<Partner>(),
        ["venues"] = await fixture.CountAsync<Venue>(),
        ["pitches"] = await fixture.CountAsync<Pitch>(),
        ["pitchPhotos"] = await fixture.CountAsync<PitchPhoto>(),
        ["openingHours"] = await fixture.CountAsync<OpeningHour>(),
        ["files"] = await fixture.CountAsync<StoredFile>(),
        ["stores"] = await fixture.CountAsync<Store>(),
        ["products"] = await fixture.CountAsync<Product>(),
        ["variants"] = await fixture.CountAsync<ProductVariant>(),
        ["sellers"] = await fixture.CountAsync<Seller>(),
        ["designs"] = await fixture.CountAsync<Design>(),
        ["designColors"] = await fixture.CountAsync<DesignColor>(),
        ["orders"] = await fixture.CountAsync<Order>(),
        ["orderItems"] = await fixture.CountAsync<OrderItem>(),
        ["tournaments"] = await fixture.CountAsync<Tournament>(),
        ["teams"] = await fixture.CountAsync<TournamentTeam>(),
        ["posts"] = await fixture.CountAsync<Post>(),
        ["agents"] = await fixture.CountAsync<Agent>(),
        ["agentPermissions"] = await fixture.CountAsync<AgentPermission>(),
        ["positions"] = await fixture.CountAsync<OrgPosition>(),
        ["staffLimits"] = await fixture.CountAsync<StaffLimit>(),
    };
}
