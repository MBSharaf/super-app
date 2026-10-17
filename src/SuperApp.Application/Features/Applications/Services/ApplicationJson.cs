using System.Text.Json;
using SuperApp.Application.Features.Applications.Dtos;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Applications.Services;

/// <summary>Answers are stored as JSON (auth.Applications.AnswersJson) with the national ID masked; this reads and writes them.</summary>
internal static class ApplicationJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write<T>(T answers) => JsonSerializer.Serialize(answers, Options);

    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException("Application answers are empty.");

    public static T Read<T>(JsonElement json) => json.Deserialize<T>(Options)
        ?? throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("answers", "The answers are missing.")]);

    /// <summary>"29801011234567" → "••••••••••4567": the full number is kept only in the encrypted ID check.</summary>
    public static string Mask(string nationalId) => new string('•', Math.Max(0, nationalId.Length - 4)) + nationalId[^4..];

    public static ApplicationResponse ToResponse(PartnerApplication app, ApplicantResponse applicant) => new(
        app.Code,
        app.Type.ToString(),
        app.Status.ToString(),
        JsonDocument.Parse(app.AnswersJson).RootElement.Clone(),
        app.RejectReason,
        app.Files.Select(f => new ApplicationFileResponse(f.FileId, f.Purpose.ToString(), $"/api/v1/files/{f.FileId}")).ToList(),
        applicant,
        app.CreatedAt,
        app.ReviewedAt);

    /// <summary>The agreement each applicant accepts, and the permission that decides the application.</summary>
    public static (string Agreement, string Permission, string Label) RulesOf(ApplicationType type) => type switch
    {
        ApplicationType.PitchOwner => ("partner", "approvals.partners", "pitch partner"),
        ApplicationType.Seller => ("seller", "approvals.sellers", "seller"),
        _ => ("partner", "approvals.factories", "factory"),
    };
}
