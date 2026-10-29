namespace CompiladorDrone.Modelos;

public sealed class Drone
{
    public string Id { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Fabricante { get; set; } = string.Empty;
    public string TipoSensor { get; set; } = string.Empty;
    public double ResolucaoMegapixels { get; set; }
    public double GsdMinimoCmPorPixel { get; set; }
    public double VelocidadeMaximaKmh { get; set; }
    public double AltitudeOperacionalMinM { get; set; }
    public double AltitudeOperacionalMaxM { get; set; }
    public double TetoVooM { get; set; }
    public double AlcanceKm { get; set; }
    public double CapacidadeBateriaMah { get; set; }
    public double TempoVooMaxMin { get; set; }
    public double ResistenciaVentoKmh { get; set; }
    public double ProtecaoUmidadePercentual { get; set; }
    public bool ProtecaoChuva { get; set; }
    public bool SistemaAntiColisao { get; set; }
    public bool TerrainFollowing { get; set; }
    public bool Rth { get; set; }

    public override string ToString() =>
        $"{Id} — {Modelo} ({TipoSensor})";
}
