using Microsoft.AspNetCore.Mvc;
using SuperApp.Application.Common.Responses;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Base for every controller. Controllers stay thin: read the request, call one service, wrap the result in the
/// unified response. No business logic here (rule: "routes carry no logic").
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected OkObjectResult Ok<T>(T data, string? message = null) => base.Ok(ApiResponse.Ok(data, message));

    protected OkObjectResult OkPage<T>(PagedResult<T> page) =>
        base.Ok(ApiResponse.Ok(page.Items, meta: page.Meta));

    protected ObjectResult Created<T>(T data, string? message = null) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(data, message));
}
