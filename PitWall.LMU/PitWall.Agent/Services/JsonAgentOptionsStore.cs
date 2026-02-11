using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services;

public sealed class JsonAgentOptionsStore : IAgentOptionsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonAgentOptionsStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task SaveAsync(AgentOptions options, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new PersistedAgentOptions(options);
        var json = JsonSerializer.Serialize(payload, Options);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    private sealed record PersistedAgentOptions(AgentOptions Agent);
}
