using CompiladorDrone.Modelos;
using System.IO;
using System.Text.Json;

namespace CompiladorDrone.Pipeline;

public sealed class ResultadoGeoGIS
{
    public Fazenda? Fazenda { get; init; }
    public double? DistanciaKm { get; init; }
    public string? Erro { get; init; }
}

public sealed class ServicoGeoGIS
{
    public const double RaioMaximoCoordenadasKm = 25.0;
    public IReadOnlyList<Fazenda> Fazendas { get; }

    public ServicoGeoGIS(string caminhoJson)
    {
        if (!File.Exists(caminhoJson))
            throw new FileNotFoundException("A base geográfica não foi encontrada.", caminhoJson);

        var json = File.ReadAllText(caminhoJson);
        Fazendas = JsonSerializer.Deserialize<List<Fazenda>>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? [];
    }

    public ResultadoGeoGIS Consultar(string? id, double? latitude, double? longitude)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var porId = Fazendas.FirstOrDefault(f =>
                string.Equals(f.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
            return porId is null
                ? new ResultadoGeoGIS { Erro = $"A fazenda '{id}' não está cadastrada." }
                : new ResultadoGeoGIS { Fazenda = porId, DistanciaKm = 0 };
        }

        if (!latitude.HasValue || !longitude.HasValue)
            return new ResultadoGeoGIS { Erro = "Informe FAZENDA ou o par LATITUDE/LONGITUDE." };

        var maisProxima = Fazendas
            .Select(f => new
            {
                Fazenda = f,
                Distancia = DistanciaHaversineKm(latitude.Value, longitude.Value, f.LatitudeCentral, f.LongitudeCentral)
            })
            .OrderBy(x => x.Distancia)
            .FirstOrDefault();

        if (maisProxima is null || maisProxima.Distancia > RaioMaximoCoordenadasKm)
        {
            var distancia = maisProxima?.Distancia.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "indisponível";
            return new ResultadoGeoGIS
            {
                Erro = $"Nenhuma propriedade foi encontrada em um raio de {RaioMaximoCoordenadasKm:F0} km (mais próxima: {distancia} km)."
            };
        }

        return new ResultadoGeoGIS
        {
            Fazenda = maisProxima.Fazenda,
            DistanciaKm = maisProxima.Distancia
        };
    }

    private static double DistanciaHaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double raioTerraKm = 6371.0;
        var dLat = GrausParaRadianos(lat2 - lat1);
        var dLon = GrausParaRadianos(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(GrausParaRadianos(lat1)) *
                Math.Cos(GrausParaRadianos(lat2)) *
                Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * raioTerraKm * Math.Asin(Math.Sqrt(a));
    }

    private static double GrausParaRadianos(double graus) => graus * Math.PI / 180.0;
}