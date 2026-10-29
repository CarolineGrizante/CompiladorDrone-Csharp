namespace CompiladorDrone.Modelos;

public sealed class Simbolo
{
    public string Parametro { get; init; } = string.Empty;
    public string Valor { get; init; } = string.Empty;
    public string Unidade { get; init; } = "—";
    public string TokenNormalizado { get; init; } = string.Empty;
    public int Linha { get; init; }
    public int Coluna { get; init; }

    public static Simbolo APartirDe(ComandoMissao comando) => new()
    {
        Parametro = comando.NomeOriginal,
        Valor = comando.Valor,
        Unidade = comando.Unidade ?? "—",
        TokenNormalizado = comando.ParametroToken.Tipo.ToString(),
        Linha = comando.Linha,
        Coluna = comando.Coluna
    };
}