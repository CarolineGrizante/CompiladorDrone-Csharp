using System.Globalization;
using CompiladorDrone.Modelos;

namespace CompiladorDrone.Modelos;

public sealed class ComandoMissao
{
    public required Token ParametroToken { get; init; }
    public required Token ValorToken { get; init; }
    public Token? UnidadeToken { get; init; }

    public string NomeOriginal => ParametroToken.Lexema;
    public string NomeNormalizado => ParametroToken.Normalizado.ToUpperInvariant();
    public string Valor => ValorToken.Lexema;
    public string? Unidade => UnidadeToken?.Lexema;
    public int Linha => ParametroToken.Linha;
    public int Coluna => ParametroToken.Coluna;

    public bool TentaObterNumero(out double valor) =>
        double.TryParse(Valor, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
}

public sealed class Missao
{
    public string Cabecalho { get; init; } = string.Empty;
    public List<ComandoMissao> Comandos { get; } = [];

    public ComandoMissao? ObterPrimeiro(string nomeNormalizado) =>
        Comandos.FirstOrDefault(c =>
            string.Equals(c.NomeNormalizado, nomeNormalizado, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<ComandoMissao> ObterTodos(string nomeNormalizado) =>
        Comandos.Where(c =>
            string.Equals(c.NomeNormalizado, nomeNormalizado, StringComparison.OrdinalIgnoreCase)).ToList();

    public bool TentaObter(string nomeNormalizado, out ComandoMissao comando)
    {
        comando = ObterPrimeiro(nomeNormalizado)!;
        return comando is not null;
    }
}

public sealed class Fazenda
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string CulturaPredominante { get; set; } = string.Empty;
    public double LatitudeCentral { get; set; }
    public double LongitudeCentral { get; set; }
    public double AlturaMaiorObstaculoM { get; set; }
    public string TipoObstaculo { get; set; } = string.Empty;
    public string Relevo { get; set; } = string.Empty;

    public override string ToString() =>
        $"{Id} — {Nome} ({Relevo})";
}