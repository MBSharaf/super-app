using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Lookups;

namespace SuperApp.Api.Controllers;

/// <summary>Public reference data for forms and pages (API §2.4). Cached for one hour.</summary>
[Route("api/v1/lookups")]
[OutputCache(PolicyName = LookupsController.CachePolicy)]
public sealed class LookupsController(ILookupsReader lookups) : ApiControllerBase
{
    public const string CachePolicy = "lookups";

    [HttpGet("governorates")]
    public async Task<ActionResult<object>> Governorates(CancellationToken ct) => Ok(await lookups.GovernoratesAsync(ct));

    [HttpGet("governorates/{id:int}/areas")]
    public async Task<ActionResult<object>> Areas(int id, CancellationToken ct) => Ok(await lookups.AreasAsync(id, ct));

    /// <summary>All areas, for pickers that show every governorate at once.</summary>
    [HttpGet("areas")]
    public async Task<ActionResult<object>> AllAreas(CancellationToken ct) => Ok(await lookups.AreasAsync(null, ct));

    [HttpGet("facilities")]
    public async Task<ActionResult<object>> Facilities(CancellationToken ct) => Ok(await lookups.FacilitiesAsync(ct));

    [HttpGet("cancellation-policies")]
    public async Task<ActionResult<object>> CancellationPolicies(CancellationToken ct) => Ok(await lookups.CancellationPoliciesAsync(ct));

    [HttpGet("product-categories")]
    public async Task<ActionResult<object>> ProductCategories(CancellationToken ct) => Ok(await lookups.ProductCategoriesAsync(ct));

    [HttpGet("design-categories")]
    public async Task<ActionResult<object>> DesignCategories(CancellationToken ct) => Ok(await lookups.DesignCategoriesAsync(ct));

    [HttpGet("seller-levels")]
    public async Task<ActionResult<object>> SellerLevels(CancellationToken ct) => Ok(await lookups.SellerLevelsAsync(ct));

    [HttpGet("settings/public")]
    public async Task<ActionResult<object>> PublicSettings(CancellationToken ct) => Ok(await lookups.PublicSettingsAsync(ct));

    /// <summary>?year=2026 — Egyptian public holidays; estimated Islamic dates are flagged.</summary>
    [HttpGet("holidays")]
    public async Task<ActionResult<object>> Holidays([FromQuery] int? year, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        if (y is < 2020 or > 2100)
        {
            throw new BadRequestException("The year must be between 2020 and 2100.", "bad_year");
        }
        return Ok(await lookups.HolidaysAsync(y, ct));
    }
}
