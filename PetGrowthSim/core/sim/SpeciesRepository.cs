using System.Text.Json;
using PetGrowthSim.Core.Config;

namespace PetGrowthSim.Core.Sim;

public sealed class SpeciesRepository
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PetSpeciesConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Species config not found: {path}");

        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<PetSpeciesConfig>(json, _jsonOptions)
                  ?? throw new InvalidOperationException("Failed to deserialize species config.");

        if (string.IsNullOrWhiteSpace(cfg.PetId))
            throw new InvalidOperationException("petId is required.");

        return cfg;
    }
}
