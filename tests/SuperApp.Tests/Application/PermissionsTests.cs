using Shouldly;
using SuperApp.Application.Security;

namespace SuperApp.Tests.Application;

/// <summary>The permission catalogue stays clean: unique codes, every code in "area.action" form.</summary>
public sealed class PermissionsTests
{
    [Fact]
    public void Codes_are_unique()
    {
        Permissions.All.Select(p => p.Code).ShouldBeUnique();
    }

    [Fact]
    public void Codes_are_lowercase_dotted_and_named_in_both_languages()
    {
        Permissions.All.ShouldAllBe(p => p.Code.Contains('.') && p.Code == p.Code.Trim());
        Permissions.All.ShouldAllBe(p => !string.IsNullOrWhiteSpace(p.NameEn) && !string.IsNullOrWhiteSpace(p.NameAr));
    }
}
