namespace CompiladorDrone.Modelos;

// Categorias produzidas pelo analisador léxico. O prefixo TK_ mantém a nomenclatura usada na especificação formal da DSL.

public enum TokenType
{
    TK_DEFINIR_MISSAO,
    TK_DRONE,
    TK_VENTO,
    TK_UMIDADE,
    TK_ILUMINACAO,
    TK_NUVENS,
    TK_CHUVA,
    TK_ALTITUDE,
    TK_GSD,
    TK_SOBREPOSICAO,
    TK_SOBREPOSICAO_FRONTAL,
    TK_SOBREPOSICAO_LATERAL,
    TK_VELOCIDADE,
    TK_AREA,
    TK_BATERIA,
    TK_RESERVA_BATERIA,
    TK_SENSOR,
    TK_RELEVO,
    TK_TERRAIN_FOLLOWING,
    TK_REGULAMENTACAO,
    TK_OPERACAO,
    TK_HOME_LATITUDE,
    TK_HOME_LONGITUDE,
    TK_RAIO_MISSAO,
    TK_RTH,
    TK_DESVIO_OBSTACULOS,
    TK_IDENTIFICADOR,
    TK_NUMERO,
    TK_UNIDADE,
    TK_ATRIBUICAO,
    TK_STATUS,
    TK_FIM_ARQUIVO
}

public sealed record Token(
    TokenType Tipo,
    string Lexema,
    int Linha,
    int Coluna,
    string? LexemaNormalizado = null)
{
    public string Normalizado => LexemaNormalizado ?? Lexema;

    public override string ToString() =>
        $"{Linha}:{Coluna}  {Tipo,-30}  {Lexema}";
}