using SuperApp.Application.Features.Auth.Dtos;

namespace SuperApp.Application.Features.Auth.Services;

/// <summary>"Continue with Google / Facebook" (API §2.1).</summary>
public interface IExternalSignInService
{
    ExternalProvidersResponse Providers();

    Task<SignInResponse> SignInAsync(ExternalSignInRequest request, CancellationToken ct);
}
