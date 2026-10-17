using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperApp.Infrastructure.Persistence.Seed;

/// <summary>Reads a seed file embedded from <c>Persistence/Seed/Data</c>, e.g. <c>Read&lt;T&gt;("Reference/roles.json")</c>.</summary>
public static class SeedFile
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static T Read<T>(string relativePath)
    {
        var assembly = typeof(SeedFile).Assembly;
        var name = $"{assembly.GetName().Name}.Persistence.Seed.Data.{relativePath.Replace('/', '.')}";
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Seed file '{relativePath}' is not embedded.");
        return JsonSerializer.Deserialize<T>(stream, Json)
            ?? throw new InvalidOperationException($"Seed file '{relativePath}' is empty.");
    }

    /// <summary>A JSON value as text, for the JSON columns (settings values, config, duties).</summary>
    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Json);
}
