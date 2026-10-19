using System.Security.Cryptography;
using System.Text;
using SuperApp.Domain.Entities.Ai;

namespace SuperApp.Application.Features.Agents;

/// <summary>Agents, their keys, tasks and runs (ai.* tables).</summary>
public interface IAgentRepository
{
    Task<IReadOnlyList<AgentDto>> ListAsync(DateTime now, CancellationToken ct);

    Task<AgentDto?> CardAsync(long id, DateTime now, CancellationToken ct);

    Task<AgentDetailDto?> DetailAsync(long id, DateTime now, CancellationToken ct);

    Task<Agent?> GetAsync(long id, CancellationToken ct);

    Task<bool> NameTakenAsync(string name, long? exceptId, CancellationToken ct);

    Task<bool> PositionExistsAsync(long positionId, CancellationToken ct);

    /// <summary>True when another agent already sits in the position.</summary>
    Task<bool> PositionTakenAsync(long positionId, long? exceptAgentId, CancellationToken ct);

    /// <summary>Makes the agent's service account (no password, cannot sign in with the site) and returns its id.</summary>
    Task<Guid> CreateServiceUserAsync(string agentName, CancellationToken ct);

    Task SetServiceUserActiveAsync(Guid userId, bool active, CancellationToken ct);

    /// <summary>Replaces the agent's permissions with these codes.</summary>
    Task SetPermissionsAsync(Agent agent, IReadOnlyList<string> codes, CancellationToken ct);

    Task ReplaceTasksAsync(long agentId, IReadOnlyList<AgentTask> tasks, CancellationToken ct);

    void Add(Agent agent);

    void Add(ApiKey key);

    void Add(AgentRun run);

    Task<ApiKey?> KeyAsync(long agentId, long keyId, CancellationToken ct);

    Task<bool> PrefixTakenAsync(string prefix, CancellationToken ct);

    Task<int> RevokeKeysAsync(long agentId, DateTime now, CancellationToken ct);

    /// <summary>Emergency stop / release for every agent at once; returns how many changed.</summary>
    Task<int> SetAllStatusAsync(Domain.Enums.AgentStatus from, Domain.Enums.AgentStatus to, string? reason, CancellationToken ct);

    Task<IReadOnlyList<AgentTaskDto>> TasksAsync(long agentId, CancellationToken ct);

    Task<AgentRun?> RunAsync(long agentId, long runId, CancellationToken ct);

    Task<bool> TaskBelongsAsync(long agentId, long taskId, CancellationToken ct);

    Task<decimal> CostThisMonthAsync(long agentId, DateTime now, CancellationToken ct);
}

/// <summary>Looks keys up for the API-key sign-in (by prefix) and remembers when a key was last used.</summary>
public interface IAgentKeyStore
{
    Task<AgentKeyIdentity?> FindAsync(string prefix, CancellationToken ct);

    Task TouchAsync(long keyId, DateTime now, CancellationToken ct);
}

/// <summary>The agent behind the current request, or null for people and guests.</summary>
public interface ICurrentAgent
{
    AgentCaller? Caller { get; }
}

/// <summary>
/// Agent API keys: <c>sa_</c> + 40 random characters. Only the SHA-256 hash and the first 8 characters (the prefix,
/// used to find the key) are stored; the key is shown once. The per-key limit is kept in the JSON column <c>Scopes</c> as <c>{"rate":600}</c>
/// until the schema gets its own column (see PROGRESS "Schema requests").
/// </summary>
public static class ApiKeys
{
    public const string Header = "X-Api-Key";
    public const string Start = "sa_";
    public const int PrefixLength = 8;
    public const int DefaultRatePerMinute = 600;
    private const string Alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string NewKey() =>
        Start + new string(Enumerable.Range(0, 40).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());

    public static string PrefixOf(string key) => key.Length >= PrefixLength ? key[..PrefixLength] : key;

    public static byte[] Hash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    public static bool Matches(string key, byte[] hash) => CryptographicOperations.FixedTimeEquals(Hash(key), hash);

    public static bool LooksValid(string? key) => key is { Length: 43 } && key.StartsWith(Start, StringComparison.Ordinal);

    public static string ScopesFor(int ratePerMinute) => $"{{\"rate\":{ratePerMinute}}}";

    public static int RateOf(string? scopes)
    {
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(scopes ?? "{}");
            return json.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && json.RootElement.TryGetProperty("rate", out var rate)
                && rate.TryGetInt32(out var n) && n > 0 ? n : DefaultRatePerMinute;
        }
        catch (System.Text.Json.JsonException)
        {
            return DefaultRatePerMinute;
        }
    }
}
