using System.IO;
using System.Text.Json;
using CompiladorDrone.Modelos;

namespace CompiladorDrone.Pipeline;

public sealed class ServicoDrones
{
    private readonly IReadOnlyList<Drone> _drones;

    public ServicoDrones(string caminhoJson)
    {
        if (!File.Exists(caminhoJson))
            throw new FileNotFoundException("A base de drones não foi encontrada.", caminhoJson);

        var json = File.ReadAllText(caminhoJson);
        _drones = JsonSerializer.Deserialize<List<Drone>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public Drone? Consultar(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : _drones.FirstOrDefault(d =>
                string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Drone> Todos => _drones;
}