using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;

namespace Nicotine.Server;

public class NicotineState
{
    [JsonPropertyName("addiction")] public double Addiction { get; set; }

    [JsonPropertyName("sinceLastSmoke")] public double SinceLastSmoke { get; set; } = -1;

    [JsonPropertyName("smoked")] public int Smoked { get; set; }
    [JsonPropertyName("updatedAt")] public long UpdatedAt { get; set; }
}

public sealed class NicotineSaveRequest : NicotineState, IRequestData;

[Injectable(InjectionType.Singleton)]
public sealed class NicotineStore(NicotineContext context)
{
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public NicotineState Load(string profileId)
    {
        var path = PathFor(profileId);
        lock (_locks.GetOrAdd(profileId, _ => new object()))
        {
            try
            {
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<NicotineState>(File.ReadAllText(path), NicotineJson.Read) ?? new NicotineState()
                    : new NicotineState();
            }
            catch (Exception)
            {
                return new NicotineState();
            }
        }
    }

    public NicotineState Save(string profileId, NicotineState incoming)
    {
        var state = new NicotineState
        {
            Addiction = Math.Clamp(double.IsFinite(incoming.Addiction) ? incoming.Addiction : 0, 0, 100),
            SinceLastSmoke = double.IsFinite(incoming.SinceLastSmoke) ? Math.Max(-1, incoming.SinceLastSmoke) : -1,
            Smoked = Math.Max(0, incoming.Smoked),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        var path = PathFor(profileId);
        lock (_locks.GetOrAdd(profileId, _ => new object()))
        {
            Directory.CreateDirectory(context.DataDirectory);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state, NicotineJson.Write));
            File.Move(temp, path, overwrite: true);
        }

        return state;
    }

    private string PathFor(string profileId)
    {
        var safe = new string(profileId.Where(char.IsAsciiLetterOrDigit).ToArray());
        return Path.Combine(context.DataDirectory, (safe.Length > 0 ? safe : "unknown") + ".json");
    }
}
