using CompiladorDrone.Modelos;

namespace CompiladorDrone.Pipeline;

public sealed class ResultadoSintatico
{
    public Missao Missao { get; set; } = new();
    public List<Diagnostico> Diagnosticos { get; } = [];
}

// Parser descendente recursivo. 
// A decisão é exclusivamente sintática: a validade de unidades, obstáculos e autonomia fica para o analisador semântico.
public sealed class Parser
{
    private IReadOnlyList<Token> _tokens = [];
    private int _indice;
    private ResultadoSintatico _resultado = new();

    public ResultadoSintatico Analisar(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
        _indice = 0;
        _resultado = new ResultadoSintatico();

        if (Atual.Tipo != TokenType.TK_DEFINIR_MISSAO)
        {
            Erro(
                "SYN001",
                "Era esperado o cabeçalho TK_DEFINIR_MISSAO (por exemplo, 'DEFINIR MISSAO').",
                Atual);
            return _resultado;
        }

        _resultado.Missao = new Missao { Cabecalho = Atual.Lexema };
        Avancar();
        Comandos();
        return _resultado;
    }

    private void Comandos()
    {
        while (Atual.Tipo != TokenType.TK_FIM_ARQUIVO)
        {
            if (!EhInicioDeParametro(Atual.Tipo))
            {
                Erro(
                    "SYN002",
                    "Era esperado um parâmetro de missão ou o fim do arquivo.",
                    Atual);
                Sincronizar();
                continue;
            }

            Comando();
        }
    }

    private void Comando()
    {
        var parametro = Atual;
        Avancar();

        if (Atual.Tipo != TokenType.TK_ATRIBUICAO)
        {
            Erro("SYN003", "Esperado token TK_ATRIBUICAO (=).", Atual, parametro);
            Sincronizar();
            return;
        }
        Avancar();

        if (!EhInicioDeValor(Atual.Tipo))
        {
            Erro("SYN004", "Esperado um valor após o operador de atribuição.", Atual, parametro);
            Sincronizar();
            return;
        }

        var valor = Atual;
        Avancar();
        Token? unidade = null;
        if (Atual.Tipo == TokenType.TK_UNIDADE)
        {
            unidade = Atual;
            Avancar();
        }

        _resultado.Missao.Comandos.Add(new ComandoMissao
        {
            ParametroToken = parametro,
            ValorToken = valor,
            UnidadeToken = unidade
        });
    }

    private void Sincronizar()
    {
        while (Atual.Tipo != TokenType.TK_FIM_ARQUIVO && !EhInicioDeParametro(Atual.Tipo))
            Avancar();
    }

    private static bool EhInicioDeParametro(TokenType tipo) => tipo is
        TokenType.TK_DRONE or
        TokenType.TK_VENTO or
        TokenType.TK_UMIDADE or
        TokenType.TK_ILUMINACAO or
        TokenType.TK_NUVENS or
        TokenType.TK_CHUVA or
        TokenType.TK_ALTITUDE or
        TokenType.TK_GSD or
        TokenType.TK_SOBREPOSICAO or
        TokenType.TK_SOBREPOSICAO_FRONTAL or
        TokenType.TK_SOBREPOSICAO_LATERAL or
        TokenType.TK_VELOCIDADE or
        TokenType.TK_AREA or
        TokenType.TK_BATERIA or
        TokenType.TK_RESERVA_BATERIA or
        TokenType.TK_SENSOR or
        TokenType.TK_RELEVO or
        TokenType.TK_TERRAIN_FOLLOWING or
        TokenType.TK_REGULAMENTACAO or
        TokenType.TK_OPERACAO or
        TokenType.TK_HOME_LATITUDE or
        TokenType.TK_HOME_LONGITUDE or
        TokenType.TK_RAIO_MISSAO or
        TokenType.TK_RTH or
        TokenType.TK_DESVIO_OBSTACULOS or
        TokenType.TK_IDENTIFICADOR;

    private static bool EhInicioDeValor(TokenType tipo) => tipo is
        TokenType.TK_NUMERO or TokenType.TK_IDENTIFICADOR or TokenType.TK_STATUS;

    private Token Atual =>
        _tokens.Count == 0
            ? new Token(TokenType.TK_FIM_ARQUIVO, string.Empty, 1, 1)
            : _tokens[Math.Min(_indice, _tokens.Count - 1)];

    private void Avancar()
    {
        if (_indice < _tokens.Count - 1)
            _indice++;
    }

    private void Erro(string codigo, string mensagem, Token local, Token? parametro = null) =>
        _resultado.Diagnosticos.Add(new Diagnostico
        {
            Fase = "SINTÁTICA",
            Severidade = SeveridadeDiagnostico.Erro,
            Codigo = codigo,
            Mensagem = mensagem,
            Linha = local.Linha,
            Coluna = local.Coluna,
            Parametro = parametro?.Normalizado
        });
}