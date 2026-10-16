namespace SuperApp.Application.Common.Exceptions;

/// <summary>
/// Base of the exceptions services throw on purpose. The global error handler turns each one into the unified
/// response with the right HTTP status (STRUCTURE §2.2). <see cref="Code"/> is a machine code such as "slot_taken".
/// </summary>
public abstract class AppException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>401 — not signed in, session expired, bad API key.</summary>
public sealed class UnauthorizedException(string message = "Please sign in first.", string code = "unauthorized")
    : AppException(message, code);

/// <summary>403 — signed in but not allowed (role, permission, two-factor missing).</summary>
public sealed class ForbiddenException(string message = "You are not allowed to do this.", string code = "forbidden")
    : AppException(message, code);

/// <summary>404 — the record does not exist or the caller may not see it (ownership is never revealed).</summary>
public sealed class NotFoundException(string message = "Not found.", string code = "not_found")
    : AppException(message, code);

/// <summary>409 — state clash: hour already booked, stock gone, record changed by someone else.</summary>
public sealed class ConflictException(string message, string code = "conflict")
    : AppException(message, code);

/// <summary>422 — valid input that breaks a business rule (below the base price, withdrawal under the minimum…).</summary>
public sealed class BusinessRuleException(string message, string code = "business_rule")
    : AppException(message, code);

/// <summary>423 — the area is switched off by a CEO kill switch or maintenance mode.</summary>
public sealed class FeatureDisabledException(string message = "This is paused for now. Please try again later.", string code = "feature_disabled")
    : AppException(message, code);
