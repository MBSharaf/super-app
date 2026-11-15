using Microsoft.Extensions.Configuration;
using Shouldly;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Infrastructure.Services;

namespace SuperApp.Tests.Application;

/// <summary>Tournaments are "coming soon": the server refuses team registrations until Features:TournamentsOpen is true.</summary>
public sealed class TournamentsSoonTests
{
    private static FeatureFlags Flags(string? open) => new(null!, new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Features:TournamentsOpen"] = open }).Build());

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task Registration_is_closed_while_coming_soon(string? open)
    {
        var error = await Should.ThrowAsync<FeatureDisabledException>(() => Flags(open).EnsureOnAsync(KillSwitches.TournamentRegistration, CancellationToken.None));
        error.Code.ShouldBe("tournaments_soon");
    }

    [Fact]
    public async Task Registration_opens_with_the_setting() =>
        (await Flags("true").IsOnAsync(KillSwitches.TournamentRegistration, CancellationToken.None)).ShouldBeTrue();
}
