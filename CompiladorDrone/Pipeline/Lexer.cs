using System.Globalization;
using CompiladorDrone.Modelos;

namespace CompiladorDrone.Pipeline;

public sealed class ResultadoLexico
{
    public List<Token> Tokens { get; } = [];
    public List<Diagnostico> Diagnosticos { get; } = [];
}

// Lexer da DSL. A implementação é equivalente ao AFD descrito na especificação: identifica nomes, números, unidades, '=' e rejeita caracteres fora de Sigma.

public sealed class Lexer
{
    private string _fonte = string.Empty;
    private int _indice;
    private int _linha;
    private int _coluna;

    private static readonly Dictionary<string, TokenType> SinonimosParametros =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DRONE"] = TokenType.TK_DRONE,
            ["MODELO_DRONE"] = TokenType.TK_DRONE,
            ["AERONAVE"] = TokenType.TK_DRONE,
            ["VENTO"] = TokenType.TK_VENTO,
            ["VELOCIDADE_VENTO"] = TokenType.TK_VENTO,
            ["RAJADA"] = TokenType.TK_VENTO,
            ["UMIDADE"] = TokenType.TK_UMIDADE,
            ["UMIDADE_AR"] = TokenType.TK_UMIDADE,
            ["HUMIDADE"] = TokenType.TK_UMIDADE,
            ["ILUMINACAO"] = TokenType.TK_ILUMINACAO,
            ["LUMINOSIDADE"] = TokenType.TK_ILUMINACAO,
            ["SOL"] = TokenType.TK_ILUMINACAO,
            ["NUVENS"] = TokenType.TK_NUVENS,
            ["NEBULOSIDADE"] = TokenType.TK_NUVENS,
            ["COBERTURA_NUVENS"] = TokenType.TK_NUVENS,
            ["CHUVA"] = TokenType.TK_CHUVA,
            ["PRECIPITACAO"] = TokenType.TK_CHUVA,
            ["ALTITUDE"] = TokenType.TK_ALTITUDE,
            ["ALTURA"] = TokenType.TK_ALTITUDE,
            ["ALTURA_VOO"] = TokenType.TK_ALTITUDE,
            ["GSD"] = TokenType.TK_GSD,
            ["RESOLUCAO_GSD"] = TokenType.TK_GSD,
            ["SOBREPOSICAO"] = TokenType.TK_SOBREPOSICAO,
            ["OVERLAP"] = TokenType.TK_SOBREPOSICAO,
            ["SOBREPOSICAO_FOTOS"] = TokenType.TK_SOBREPOSICAO,
            ["SOBREPOSICAO_FRONTAL"] = TokenType.TK_SOBREPOSICAO_FRONTAL,
            ["OVERLAP_FRONTAL"] = TokenType.TK_SOBREPOSICAO_FRONTAL,
            ["SOBREPOSICAO_LONGITUDINAL"] = TokenType.TK_SOBREPOSICAO_FRONTAL,
            ["SOBREPOSICAO_LATERAL"] = TokenType.TK_SOBREPOSICAO_LATERAL,
            ["OVERLAP_LATERAL"] = TokenType.TK_SOBREPOSICAO_LATERAL,
            ["VELOCIDADE"] = TokenType.TK_VELOCIDADE,
            ["VELOCIDADE_VOO"] = TokenType.TK_VELOCIDADE,
            ["AREA"] = TokenType.TK_AREA,
            ["AREA_TERRENO"] = TokenType.TK_AREA,
            ["TAMANHO_FAZENDA"] = TokenType.TK_AREA,
            ["BATERIA"] = TokenType.TK_BATERIA,
            ["CAPACIDADE_BATERIA"] = TokenType.TK_BATERIA,
            ["CARGA"] = TokenType.TK_BATERIA,
            ["RESERVA_BATERIA"] = TokenType.TK_RESERVA_BATERIA,
            ["BATERIA_RESERVA"] = TokenType.TK_RESERVA_BATERIA,
            ["SENSOR"] = TokenType.TK_SENSOR,
            ["CAMERA"] = TokenType.TK_SENSOR,
            ["TIPO_CAMERA"] = TokenType.TK_SENSOR,
            ["RELEVO"] = TokenType.TK_RELEVO,
            ["TERRAIN_FOLLOWING"] = TokenType.TK_TERRAIN_FOLLOWING,
            ["ACOMPANHAMENTO_RELEVO"] = TokenType.TK_TERRAIN_FOLLOWING,
            ["REGULAMENTACAO"] = TokenType.TK_REGULAMENTACAO,
            ["SISANT"] = TokenType.TK_REGULAMENTACAO,
            ["DECEA"] = TokenType.TK_REGULAMENTACAO,
            ["LICENCA"] = TokenType.TK_REGULAMENTACAO,
            ["OPERACAO"] = TokenType.TK_OPERACAO,
            ["TIPO_OPERACAO"] = TokenType.TK_OPERACAO,
            ["VLOS"] = TokenType.TK_OPERACAO,
            ["BVLOS"] = TokenType.TK_OPERACAO,
            ["HOME_LATITUDE"] = TokenType.TK_HOME_LATITUDE,
            ["HOME_LONGITUDE"] = TokenType.TK_HOME_LONGITUDE,
            ["RAIO_MISSAO"] = TokenType.TK_RAIO_MISSAO,
            ["DISTANCIA_MISSAO"] = TokenType.TK_RAIO_MISSAO,
            ["RTH"] = TokenType.TK_RTH,
            ["RETORNO_AUTOMATICO"] = TokenType.TK_RTH,
            ["DESVIO_OBSTACULOS"] = TokenType.TK_DESVIO_OBSTACULOS,
            ["SISTEMA_OBSTACULOS"] = TokenType.TK_DESVIO_OBSTACULOS,
            ["ANTI_COLISAO"] = TokenType.TK_DESVIO_OBSTACULOS
        };

    private static readonly HashSet<string> Status = new(StringComparer.OrdinalIgnoreCase)
    {
        "ATIVO", "INATIVO", "SIM", "NAO", "SISANT_OK", "PENDENTE", "VLOS", "BVLOS"
    };

    private static readonly HashSet<string> Unidades = new(StringComparer.OrdinalIgnoreCase)
    {
        "KM/H", "MAH", "HA", "M", "%", "C", "MM/H", "MIN", "MINUTO", "MINUTOS", "KM", "CM/PIXEL", "CM/PIX", "M/PIXEL"
    };

    public ResultadoLexico Analisar(string fonte)
    {
        _fonte = fonte ?? string.Empty;
        _indice = 0;
        _linha = 1;
        _coluna = 1;

        var resultado = new ResultadoLexico();
        while (!Fim)
        {
            if (Atual is ' ' or '\t' or '\r' or '\n')
            {
                Avancar();
                continue;
            }

            var linhaInicial = _linha;
            var colunaInicial = _coluna;

            if (Atual == '=')
            {
                resultado.Tokens.Add(new Token(TokenType.TK_ATRIBUICAO, "=", linhaInicial, colunaInicial));
                Avancar();
                continue;
            }

            if (Atual == '%')
            {
                resultado.Tokens.Add(new Token(TokenType.TK_UNIDADE, "%", linhaInicial, colunaInicial, "%"));
                Avancar();
                continue;
            }

            if (Atual == '-' && ProximoEhDigito() || char.IsDigit(Atual))
            {
                var lexema = LerNumero();
                resultado.Tokens.Add(new Token(
                    TokenType.TK_NUMERO,
                    lexema,
                    linhaInicial,
                    colunaInicial,
                    lexema));
                continue;
            }

            if (EhLetraOuSublinhado(Atual))
            {
                var inicio = _indice;
                var palavra = LerIdentificadorBruto();
                var maiuscula = palavra.ToUpperInvariant();

                if (maiuscula is "DEFINIR" or "INICIAR" &&
                    TentarCompletarCabecalho(maiuscula, inicio, out var cabecalho))
                {
                    resultado.Tokens.Add(new Token(
                        TokenType.TK_DEFINIR_MISSAO,
                        cabecalho,
                        linhaInicial,
                        colunaInicial,
                        NormalizarCabecalho(cabecalho)));
                    continue;
                }

                if (maiuscula == "KM" && Atual == '/' &&
                    _indice + 1 < _fonte.Length &&
                    (_fonte[_indice + 1] == 'h' || _fonte[_indice + 1] == 'H'))
                {
                    Avancar();
                    Avancar();
                    resultado.Tokens.Add(new Token(
                        TokenType.TK_UNIDADE,
                        _fonte[inicio.._indice],
                        linhaInicial,
                        colunaInicial,
                        "km/h"));
                    continue;
                }

                if (Atual == '/' && _indice + 1 < _fonte.Length &&
                    (char.IsLetter(_fonte[_indice + 1]) || _fonte[_indice + 1] == '%'))
                {
                    Avancar();
                    while (!Fim && (char.IsLetter(Atual) || Atual == '%'))
                        Avancar();
                    var unidadeComposta = _fonte[inicio.._indice].ToUpperInvariant();
                    if (Unidades.Contains(unidadeComposta))
                    {
                        resultado.Tokens.Add(new Token(
                            TokenType.TK_UNIDADE,
                            _fonte[inicio.._indice],
                            linhaInicial,
                            colunaInicial,
                            NormalizarUnidade(unidadeComposta)));
                        continue;
                    }
                }

                if (Unidades.Contains(maiuscula))
                {
                    resultado.Tokens.Add(new Token(
                        TokenType.TK_UNIDADE,
                        palavra,
                        linhaInicial,
                        colunaInicial,
                        NormalizarUnidade(maiuscula)));
                    continue;
                }

                if (Status.Contains(maiuscula))
                {
                    resultado.Tokens.Add(new Token(
                        TokenType.TK_STATUS,
                        palavra,
                        linhaInicial,
                        colunaInicial,
                        maiuscula));
                    continue;
                }

                if (SinonimosParametros.TryGetValue(maiuscula, out var tipoParametro))
                {
                    resultado.Tokens.Add(new Token(
                        tipoParametro,
                        palavra,
                        linhaInicial,
                        colunaInicial,
                        NomeNormalizado(tipoParametro)));
                    continue;
                }

                resultado.Tokens.Add(new Token(
                    TokenType.TK_IDENTIFICADOR,
                    palavra,
                    linhaInicial,
                    colunaInicial,
                    maiuscula));
                continue;
            }

            resultado.Diagnosticos.Add(new Diagnostico
            {
                Fase = "LÉXICA",
                Severidade = SeveridadeDiagnostico.Erro,
                Codigo = "LEX001",
                Mensagem = $"O símbolo '{Atual}' não pertence ao alfabeto da DSL.",
                Linha = linhaInicial,
                Coluna = colunaInicial
            });
            Avancar();
        }

        resultado.Tokens.Add(new Token(TokenType.TK_FIM_ARQUIVO, string.Empty, _linha, _coluna));
        return resultado;
    }

    private bool Fim => _indice >= _fonte.Length;
    private char Atual => Fim ? '\0' : _fonte[_indice];

    private bool ProximoEhDigito() =>
        _indice + 1 < _fonte.Length && char.IsDigit(_fonte[_indice + 1]);

    private static bool EhLetraOuSublinhado(char caractere) =>
        caractere is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';

    private static bool EhDigito(char caractere) => caractere is >= '0' and <= '9';

    private string LerIdentificadorBruto()
    {
        var inicio = _indice;
        while (!Fim && (EhLetraOuSublinhado(Atual) || EhDigito(Atual)))
            Avancar();
        return _fonte[inicio.._indice];
    }

    private string LerNumero()
    {
        var inicio = _indice;
        if (Atual == '-')
            Avancar();
        while (!Fim && EhDigito(Atual))
            Avancar();
        if (Atual == '.' && _indice + 1 < _fonte.Length && EhDigito(_fonte[_indice + 1]))
        {
            Avancar();
            while (!Fim && EhDigito(Atual))
                Avancar();
        }
        return _fonte[inicio.._indice];
    }

    private bool TentarCompletarCabecalho(string primeiro, int inicio, out string cabecalho)
    {
        cabecalho = string.Empty;
        var separador = _indice;
        while (_indice < _fonte.Length && (_fonte[_indice] == ' ' || _fonte[_indice] == '\t'))
            Avancar();

        var inicioSegundo = _indice;
        while (_indice < _fonte.Length && (EhLetraOuSublinhado(_fonte[_indice]) || EhDigito(_fonte[_indice])))
            Avancar();
        var segundo = _fonte[inicioSegundo.._indice].ToUpperInvariant();
        var valido = (primeiro, segundo) is ("DEFINIR", "MISSAO") or
            ("DEFINIR", "VIAGEM") or ("INICIAR", "MISSAO") or ("INICIAR", "VIAGEM");

        if (!valido)
        {
            var fimTentativa = _indice;
            _indice = separador;
            _coluna -= fimTentativa - separador;
            return false;
        }

        cabecalho = _fonte[inicio.._indice];
        return true;
    }

    private void Avancar()
    {
        if (Fim)
            return;

        if (Atual == '\r')
        {
            _indice++;
            if (!Fim && Atual == '\n')
                _indice++;
            _linha++;
            _coluna = 1;
            return;
        }

        if (Atual == '\n')
        {
            _indice++;
            _linha++;
            _coluna = 1;
            return;
        }

        _indice++;
        _coluna++;
    }

    private static string NormalizarCabecalho(string cabecalho) =>
        cabecalho.Replace(" ", "_").ToUpperInvariant() switch
        {
            "DEFINIR_MISSAO" or "DEFINIR_VIAGEM" or "INICIAR_MISSAO" or "INICIAR_VIAGEM"
                => "DEFINIR_MISSAO",
            _ => "DEFINIR_MISSAO"
        };

    private static string NormalizarUnidade(string unidade) => unidade switch
    {
        "KM/H" => "km/h",
        "MM/H" => "mm/h",
        "MAH" => "mAh",
        "HA" => "ha",
        "M" => "m",
        "C" => "C",
        "MIN" or "MINUTO" or "MINUTOS" => "min",
        "KM" => "km",
        "CM/PIXEL" or "CM/PIX" => "cm/pixel",
        "M/PIXEL" => "m/pixel",
        _ => unidade
    };

    private static string NomeNormalizado(TokenType tipo) => tipo switch
    {
        TokenType.TK_DRONE => "DRONE",
        TokenType.TK_VENTO => "VENTO",
        TokenType.TK_UMIDADE => "UMIDADE",
        TokenType.TK_ILUMINACAO => "ILUMINACAO",
        TokenType.TK_NUVENS => "NUVENS",
        TokenType.TK_CHUVA => "CHUVA",
        TokenType.TK_ALTITUDE => "ALTITUDE",
        TokenType.TK_GSD => "GSD",
        TokenType.TK_SOBREPOSICAO => "SOBREPOSICAO",
        TokenType.TK_SOBREPOSICAO_FRONTAL => "SOBREPOSICAO_FRONTAL",
        TokenType.TK_SOBREPOSICAO_LATERAL => "SOBREPOSICAO_LATERAL",
        TokenType.TK_VELOCIDADE => "VELOCIDADE",
        TokenType.TK_AREA => "AREA",
        TokenType.TK_BATERIA => "BATERIA",
        TokenType.TK_RESERVA_BATERIA => "RESERVA_BATERIA",
        TokenType.TK_SENSOR => "SENSOR",
        TokenType.TK_RELEVO => "RELEVO",
        TokenType.TK_TERRAIN_FOLLOWING => "TERRAIN_FOLLOWING",
        TokenType.TK_REGULAMENTACAO => "REGULAMENTACAO",
        TokenType.TK_OPERACAO => "OPERACAO",
        TokenType.TK_HOME_LATITUDE => "HOME_LATITUDE",
        TokenType.TK_HOME_LONGITUDE => "HOME_LONGITUDE",
        TokenType.TK_RAIO_MISSAO => "RAIO_MISSAO",
        TokenType.TK_RTH => "RTH",
        TokenType.TK_DESVIO_OBSTACULOS => "DESVIO_OBSTACULOS",
        _ => tipo.ToString()
    };
}