namespace CompiladorDrone.Modelos;

public enum SeveridadeDiagnostico
{
    Informacao,
    Alerta,
    Erro,
    Critico
}

public sealed class Diagnostico
{
    public string Fase { get; init; } = string.Empty;
    public SeveridadeDiagnostico Severidade { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Mensagem { get; init; } = string.Empty;
    public int? Linha { get; init; }
    public int? Coluna { get; init; }
    public string? Parametro { get; init; }

    public override string ToString()
    {
        var posicao = Linha.HasValue ? $" (linha {Linha}, coluna {Coluna})" : string.Empty;
        var campo = string.IsNullOrWhiteSpace(Parametro) ? string.Empty : $" [{Parametro}]";
        return $"{Severidade.ToString().ToUpperInvariant()} {Codigo}{campo}{posicao}: {Mensagem}";
    }
}

public sealed class RelatorioMissao
{
    public List<Diagnostico> Diagnosticos { get; } = [];
    public List<Simbolo> Simbolos { get; } = [];
    public List<Token> Tokens { get; } = [];

    public Fazenda? Fazenda { get; set; }
    public Drone? Drone { get; set; }
    public double? AlcanceDroneKm { get; set; }
    public double? VelocidadeMaximaDroneKmh { get; set; }
    public double? TetoVooDroneM { get; set; }
    public double? ResistenciaVentoDroneKmh { get; set; }
    public double? ProtecaoUmidadeDronePercentual { get; set; }
    public double? AlturaMaiorObstaculoM { get; set; }
    public double? ConsumoEstimadoMah { get; set; }
    public double? CapacidadeBateriaMah { get; set; }
    public double? MemoriaRequeridaMb { get; set; }
    public double? CoberturaEstimadaHa { get; set; }
    public double? EficienciaCoberturaPercentual { get; set; }
    public double? GsdCmPorPixel { get; set; }
    public double? DistanciaMissaoKm { get; set; }
    public double? TempoVooMinutos { get; set; }
    public double? ReservaBateriaMah { get; set; }
    public string SensorDetectado { get; set; } = "NÃO INFORMADO";
    public string OperacaoDetectada { get; set; } = "NÃO INFORMADA";
    public bool TerrainFollowingAtivo { get; set; }
    public string NivelRisco { get; set; } = "NÃO CALCULADO";

    public bool TemErros => Diagnosticos.Any(d =>
        d.Severidade is SeveridadeDiagnostico.Erro or SeveridadeDiagnostico.Critico);

    public bool TemCritico => Diagnosticos.Any(d => d.Severidade == SeveridadeDiagnostico.Critico);

    public void Adicionar(
        string fase,
        SeveridadeDiagnostico severidade,
        string codigo,
        string mensagem,
        int? linha = null,
        int? coluna = null,
        string? parametro = null) =>
        Diagnosticos.Add(new Diagnostico
        {
            Fase = fase,
            Severidade = severidade,
            Codigo = codigo,
            Mensagem = mensagem,
            Linha = linha,
            Coluna = coluna,
            Parametro = parametro
        });
}