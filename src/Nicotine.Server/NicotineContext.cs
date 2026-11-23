using System.Text.Json;
using SPTarkov.DI.Annotations;

namespace Nicotine.Server;

[Injectable(InjectionType.Singleton)]
public sealed class NicotineContext
{
    public NicotineContext()
    {
        var location = typeof(NicotineContext).Assembly.Location;
        ModDirectory = (string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location))
                       ?? Path.Combine(AppContext.BaseDirectory, "user", "mods", "Nicotine");
        DataDirectory = Path.Combine(ModDirectory, "data");

        var path = Path.Combine(ModDirectory, "config.json");
        try
        {
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                Config = JsonSerializer.Deserialize<NicotineConfig>(text, NicotineJson.Read) ?? new NicotineConfig();

                foreach (var (name, entries) in new NicotineConfig().BuffSets)
                {
                    Config.BuffSets.TryAdd(name, entries);
                }

                var upgraded = JsonSerializer.Serialize(Config, NicotineJson.Write);
                if (!string.Equals(Normalize(upgraded), Normalize(text), StringComparison.Ordinal))
                {
                    File.WriteAllText(path, upgraded);
                    ConfigUpgraded = true;
                }
            }
            else
            {
                Config = new NicotineConfig();
                File.WriteAllText(path, JsonSerializer.Serialize(Config, NicotineJson.Write));
                ConfigCreated = true;
            }
        }
        catch (Exception ex)
        {
            ConfigError = $"config.json is invalid, using defaults: {ex.Message}";
            Config = new NicotineConfig();
        }
    }

    public string ModDirectory { get; }
    public string DataDirectory { get; }
    public NicotineConfig Config { get; }
    public bool ConfigCreated { get; }
    public bool ConfigUpgraded { get; }
    public string? ConfigError { get; }

    private static string Normalize(string json) => string.Concat(json.Where(c => !char.IsWhiteSpace(c)));
}
