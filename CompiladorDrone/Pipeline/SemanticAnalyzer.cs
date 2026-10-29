using System.Globalization;
using CompiladorDrone.Modelos;

namespace CompiladorDrone.Pipeline;

// Análise semântica da DSL. A fórmula de domínio é deliberadamente simples,
// determinística e está documentada em ANALISE_ESPECIFICACAO.md.

public sealed class SemanticAnalyzer
{
    private readonly ServicoGeoGIS _geoGis;
    private readonly ServicoDrones? _drones;

    private static readonly Dictionary<string, string> UnidadesObrigatorias =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ALTITUDE"] = "m",
            ["VELOCIDADE"] = "km/h",
            ["VENTO"] = "km/h",
            ["CHUVA"] = "mm/h",
            ["AREA"] = "ha",
            ["BATERIA"] = "mAh",
            ["RESERVA_BATERIA"] = "mAh",
            ["GSD"] = "cm/pixel",
            ["UMIDADE"] = "%",
            ["SOBREPOSICAO"] = "%",
            ["SOBREPOSICAO_FRONTAL"] = "%",
            ["SOBREPOSICAO_LATERAL"] = "%",
            ["NUVENS"] = "%",
            ["ILUMINACAO"] = "%"
        };

    private static readonly HashSet<string> ParametrosConhecidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "DRONE", "FAZENDA", "LATITUDE", "LONGITUDE", "ALTITUDE", "VELOCIDADE", "VENTO",
        "UMIDADE", "ILUMINACAO", "NUVENS", "CHUVA", "AREA", "BATERIA", "RESERVA_BATERIA",
        "SOBREPOSICAO", "SOBREPOSICAO_FRONTAL", "SOBREPOSICAO_LATERAL", "GSD", "SENSOR",
        "RELEVO", "TERRAIN_FOLLOWING", "REGULAMENTACAO", "OPERACAO", "HOME_LATITUDE",
        "HOME_LONGITUDE", "RAIO_MISSAO", "RTH", "DESVIO_OBSTACULOS"
    };

    public SemanticAnalyzer(ServicoGeoGIS geoGis, ServicoDrones? drones = null)
    {
        _geoGis = geoGis;
        _drones = drones;
    }

    public RelatorioMissao Analisar(Missao missao, IReadOnlyList<Token> tokens)
    {
        var relatorio = new RelatorioMissao();
        relatorio.Tokens.AddRange(tokens);
        relatorio.Simbolos.AddRange(missao.Comandos.Select(Simbolo.APartirDe));

        foreach (var comando in missao.Comandos)
        {
            if (!ParametrosConhecidos.Contains(comando.NomeNormalizado))
            {
                relatorio.Adicionar(
                    "SEMÂNTICA",
                    SeveridadeDiagnostico.Alerta,
                    "SEM001",
                    "Parâmetro não previsto no domínio; ele foi aceito pela gramática genérica, mas não participa dos cálculos.",
                    comando.Linha,
                    comando.Coluna,
                    comando.NomeOriginal);
            }

            ValidarUnidade(comando, relatorio);
        }

        ValidarDuplicidades(missao, relatorio);
        ValidarValores(missao, relatorio);
        AvaliarGeografia(missao, relatorio);
        AvaliarDrone(missao, relatorio);
        AvaliarMeteorologia(missao, relatorio);
        AvaliarMapeamento(missao, relatorio);
        AvaliarRegulamentacao(missao, relatorio);
        AvaliarCoberturaEAutonomia(missao, relatorio);
        AvaliarOperacaoESensores(missao, relatorio);
        DefinirNivelRisco(relatorio);
        return relatorio;
    }

    private static void ValidarUnidade(ComandoMissao comando, RelatorioMissao relatorio)
    {
        if (!UnidadesObrigatorias.TryGetValue(comando.NomeNormalizado, out var esperada))
        {
            if (comando.Unidade is not null &&
                comando.NomeNormalizado is
                     ("DRONE" or "FAZENDA" or "LATITUDE" or "LONGITUDE" or "HOME_LATITUDE" or "HOME_LONGITUDE" or
                     "SENSOR" or "RELEVO" or "TERRAIN_FOLLOWING" or "REGULAMENTACAO" or "OPERACAO" or
                     "RTH" or "DESVIO_OBSTACULOS"))
            {
                relatorio.Adicionar(
                    "SEMÂNTICA",
                    SeveridadeDiagnostico.Erro,
                    "SEM002",
                    "Este parâmetro não aceita unidade.",
                    comando.Linha,
                    comando.Coluna,
                    comando.NomeOriginal);
            }
            return;
        }

        if (comando.Unidade is null)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Erro,
                "SEM003",
                $"O parâmetro exige a unidade '{esperada}'.",
                comando.Linha,
                comando.Coluna,
                comando.NomeOriginal);
            return;
        }

        if (!string.Equals(comando.Unidade, esperada, StringComparison.OrdinalIgnoreCase))
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Erro,
                "SEM004",
                $"Unidade incompatível: '{comando.Unidade}'. Para {comando.NomeOriginal}, use '{esperada}'.",
                comando.Linha,
                comando.Coluna,
                comando.NomeOriginal);
        }
    }

    private static void ValidarDuplicidades(Missao missao, RelatorioMissao relatorio)
    {
        foreach (var grupo in missao.Comandos.GroupBy(c => c.NomeNormalizado, StringComparer.OrdinalIgnoreCase))
        {
            if (grupo.Count() > 1)
            {
                var primeiro = grupo.First();
                relatorio.Adicionar(
                    "SEMÂNTICA",
                    SeveridadeDiagnostico.Alerta,
                    "SEM005",
                    "O parâmetro aparece mais de uma vez; será utilizado o primeiro valor.",
                    primeiro.Linha,
                    primeiro.Coluna,
                    primeiro.NomeOriginal);
            }
        }
    }

    private static void ValidarValores(Missao missao, RelatorioMissao relatorio)
    {
        ValidarPositivo(missao.ObterPrimeiro("ALTITUDE"), relatorio, "ALTITUDE");
        ValidarPositivo(missao.ObterPrimeiro("VELOCIDADE"), relatorio, "VELOCIDADE");
        ValidarPositivo(missao.ObterPrimeiro("AREA"), relatorio, "AREA");
        ValidarPositivo(missao.ObterPrimeiro("BATERIA"), relatorio, "BATERIA");
        ValidarFaixa(missao.ObterPrimeiro("UMIDADE"), relatorio, "UMIDADE", 0, 100);
        ValidarFaixa(missao.ObterPrimeiro("ILUMINACAO"), relatorio, "ILUMINACAO", 0, 100);
        ValidarFaixa(missao.ObterPrimeiro("NUVENS"), relatorio, "NUVENS", 0, 100);
        ValidarFaixa(missao.ObterPrimeiro("SOBREPOSICAO"), relatorio, "SOBREPOSICAO", 0, 100);
        ValidarFaixa(missao.ObterPrimeiro("SOBREPOSICAO_FRONTAL"), relatorio, "SOBREPOSICAO_FRONTAL", 0, 100);
        ValidarFaixa(missao.ObterPrimeiro("SOBREPOSICAO_LATERAL"), relatorio, "SOBREPOSICAO_LATERAL", 0, 100);
        ValidarNaoNegativo(missao.ObterPrimeiro("CHUVA"), relatorio, "CHUVA");
        ValidarPositivo(missao.ObterPrimeiro("GSD"), relatorio, "GSD");
        ValidarPositivo(missao.ObterPrimeiro("RESERVA_BATERIA"), relatorio, "RESERVA_BATERIA");
        ValidarPositivo(missao.ObterPrimeiro("RAIO_MISSAO"), relatorio, "RAIO_MISSAO");

        var antiColisao = missao.ObterPrimeiro("DESVIO_OBSTACULOS");
        if (antiColisao is not null)
        {
            var estado = antiColisao.Valor.ToUpperInvariant();
            if (estado is not ("ATIVO" or "INATIVO" or "SIM" or "NAO"))
            {
                relatorio.Adicionar(
                    "SEMÂNTICA",
                    SeveridadeDiagnostico.Erro,
                    "SEM006",
                    "ANTI_COLISAO deve ter estado ATIVO, INATIVO, SIM ou NAO.",
                    antiColisao.Linha,
                    antiColisao.Coluna,
                    antiColisao.NomeOriginal);
            }
        }
    }

    private static void ValidarPositivo(ComandoMissao? comando, RelatorioMissao relatorio, string nome)
    {
        if (comando is null || !comando.TentaObterNumero(out var valor))
        {
            if (comando is not null)
                AdicionarNumeroInvalido(comando, relatorio);
            return;
        }

        if (valor <= 0)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Erro,
                "SEM007",
                "O valor deve ser maior que zero.",
                comando.Linha,
                comando.Coluna,
                nome);
        }
    }

    private static void ValidarFaixa(ComandoMissao? comando, RelatorioMissao relatorio, string nome, double minimo, double maximo)
    {
        if (comando is null)
            return;
        if (!comando.TentaObterNumero(out var valor))
        {
            AdicionarNumeroInvalido(comando, relatorio);
            return;
        }
        if (valor < minimo || valor > maximo)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Erro,
                "SEM008",
                $"O valor deve estar entre {minimo} e {maximo}.",
                comando.Linha,
                comando.Coluna,
                nome);
        }
    }

    private static void ValidarNaoNegativo(ComandoMissao? comando, RelatorioMissao relatorio, string nome)
    {
        if (comando is null)
            return;
        if (!comando.TentaObterNumero(out var valor))
        {
            AdicionarNumeroInvalido(comando, relatorio);
            return;
        }
        if (valor < 0)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Erro,
                "SEM010",
                $"O parâmetro {nome} não pode ser negativo.",
                comando.Linha,
                comando.Coluna,
                comando.NomeOriginal);
        }
    }

    private static void AdicionarNumeroInvalido(ComandoMissao comando, RelatorioMissao relatorio) =>
        relatorio.Adicionar(
            "SEMÂNTICA",
            SeveridadeDiagnostico.Erro,
            "SEM009",
            "O parâmetro exige um valor numérico válido.",
            comando.Linha,
            comando.Coluna,
            comando.NomeOriginal);

    private void AvaliarGeografia(Missao missao, RelatorioMissao relatorio)
    {
        var fazenda = missao.ObterPrimeiro("FAZENDA");
        var latitude = ObterNumeroOpcional(missao.ObterPrimeiro("LATITUDE"), relatorio);
        var longitude = ObterNumeroOpcional(missao.ObterPrimeiro("LONGITUDE"), relatorio);
        var consulta = _geoGis.Consultar(fazenda?.Valor, latitude, longitude);

        if (consulta.Fazenda is null)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Erro,
                "GEO001",
                consulta.Erro ?? "Não foi possível localizar a propriedade.",
                fazenda?.Linha,
                fazenda?.Coluna,
                "FAZENDA");
            return;
        }

        relatorio.Fazenda = consulta.Fazenda;
        relatorio.AlturaMaiorObstaculoM = consulta.Fazenda.AlturaMaiorObstaculoM;
        var distancia = consulta.DistanciaKm.HasValue
            ? $"; distância do centro: {consulta.DistanciaKm.Value:F2} km"
            : string.Empty;
        relatorio.Adicionar(
            "SEMÂNTICA",
            SeveridadeDiagnostico.Informacao,
            "GEO002",
            $"Propriedade localizada: {consulta.Fazenda.Id} — {consulta.Fazenda.Nome}{distancia}. Obstáculo máximo: {consulta.Fazenda.AlturaMaiorObstaculoM:F1} m.",
            fazenda?.Linha,
            fazenda?.Coluna,
            "FAZENDA");

        var altitudeComando = missao.ObterPrimeiro("ALTITUDE");
        if (altitudeComando is null || !altitudeComando.TentaObterNumero(out var altitude))
            return;

        if (altitude <= consulta.Fazenda.AlturaMaiorObstaculoM)
        {
            var anti = missao.ObterPrimeiro("DESVIO_OBSTACULOS");
            var estado = anti?.Valor.ToUpperInvariant();
            if (estado is "ATIVO" or "SIM")
            {
                relatorio.Adicionar(
                    "SEMÂNTICA",
                    SeveridadeDiagnostico.Alerta,
                    "GPS001",
                    $"A altitude ({altitude:F1} m) não supera o obstáculo registrado ({consulta.Fazenda.AlturaMaiorObstaculoM:F1} m). Anticolisão ativa: planejar desvio.",
                    altitudeComando.Linha,
                    altitudeComando.Coluna,
                    altitudeComando.NomeOriginal);
            }
            else
            {
                relatorio.Adicionar(
                    "SEMÂNTICA",
                    SeveridadeDiagnostico.Critico,
                    "GPS002",
                    $"ERRO CRÍTICO DE COLISÃO — altitude de {altitude:F1} m para obstáculo de {consulta.Fazenda.AlturaMaiorObstaculoM:F1} m com anticolisão inativa ou não informada.",
                    altitudeComando.Linha,
                    altitudeComando.Coluna,
                    altitudeComando.NomeOriginal);
            }
        }
    }

    private static double? ObterNumeroOpcional(ComandoMissao? comando, RelatorioMissao relatorio)
    {
        if (comando is null)
            return null;
        if (comando.TentaObterNumero(out var valor))
            return valor;
        AdicionarNumeroInvalido(comando, relatorio);
        return null;
    }

    private void AvaliarDrone(Missao missao, RelatorioMissao relatorio)
    {
        var comando = missao.ObterPrimeiro("DRONE");
        if (comando is null)
        {
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Informacao, "DRN000",
                "Modelo de drone não informado; a missão será avaliada apenas pelos fatores ambientais e operacionais.",
                parametro: "DRONE");
            return;
        }

        if (_drones is null)
        {
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, "DRN099",
                "O catálogo de drones não foi carregado.", comando.Linha, comando.Coluna, comando.NomeOriginal);
            return;
        }

        var drone = _drones.Consultar(comando.Valor);
        if (drone is null)
        {
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, "DRN001",
                $"Drone '{comando.Valor}' não foi encontrado no catálogo.", comando.Linha, comando.Coluna, comando.NomeOriginal);
            return;
        }

        relatorio.Drone = drone;
        relatorio.AlcanceDroneKm = drone.AlcanceKm;
        relatorio.VelocidadeMaximaDroneKmh = drone.VelocidadeMaximaKmh;
        relatorio.TetoVooDroneM = drone.TetoVooM;
        relatorio.ResistenciaVentoDroneKmh = drone.ResistenciaVentoKmh;
        relatorio.ProtecaoUmidadeDronePercentual = drone.ProtecaoUmidadePercentual;
        relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Informacao, "DRN002",
            $"Drone selecionado: {drone.Id} — {drone.Modelo}; sensor {drone.TipoSensor}; bateria {drone.CapacidadeBateriaMah:F0} mAh.",
            comando.Linha, comando.Coluna, comando.NomeOriginal);

        CompararDroneNumerico(missao, relatorio, "VELOCIDADE", drone.VelocidadeMaximaKmh,
            "DRN003", $"A velocidade da missão excede a velocidade máxima do drone ({drone.VelocidadeMaximaKmh:F1} km/h).");
        CompararDroneNumerico(missao, relatorio, "ALTITUDE", drone.TetoVooM,
            "DRN004", $"A altitude da missão excede o teto de voo do drone ({drone.TetoVooM:F1} m).");

        var altitude = missao.ObterPrimeiro("ALTITUDE");
        if (altitude is not null && altitude.TentaObterNumero(out var altitudeValor) &&
            (altitudeValor < drone.AltitudeOperacionalMinM || altitudeValor > drone.AltitudeOperacionalMaxM))
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "DRN005",
                $"Altitude {altitudeValor:F1} m fora da faixa operacional do drone ({drone.AltitudeOperacionalMinM:F1}–{drone.AltitudeOperacionalMaxM:F1} m).",
                altitude.Linha, altitude.Coluna, altitude.NomeOriginal);

        var vento = missao.ObterPrimeiro("VENTO");
        if (vento is not null && vento.TentaObterNumero(out var ventoValor) && ventoValor > drone.ResistenciaVentoKmh)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Critico, "DRN006",
                $"O vento ({ventoValor:F1} km/h) excede a resistência do drone ({drone.ResistenciaVentoKmh:F1} km/h).",
                vento.Linha, vento.Coluna, vento.NomeOriginal);

        var humidade = missao.ObterPrimeiro("UMIDADE");
        if (humidade is not null && humidade.TentaObterNumero(out var humidadeValor) && humidadeValor > drone.ProtecaoUmidadePercentual)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "DRN007",
                $"A umidade ({humidadeValor:F1}%) excede a proteção especificada do drone ({drone.ProtecaoUmidadePercentual:F1}%).",
                humidade.Linha, humidade.Coluna, humidade.NomeOriginal);

        var chuva = missao.ObterPrimeiro("CHUVA");
        if (chuva is not null && chuva.TentaObterNumero(out var chuvaValor) && chuvaValor > 0 && !drone.ProtecaoChuva)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Critico, "DRN008",
                "O drone selecionado não possui proteção contra chuva; a missão não deve operar com precipitação.",
                chuva.Linha, chuva.Coluna, chuva.NomeOriginal);

        var sensor = missao.ObterPrimeiro("SENSOR");
        if (sensor is not null && !string.Equals(sensor.Valor, drone.TipoSensor, StringComparison.OrdinalIgnoreCase))
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, "DRN009",
                $"Sensor solicitado ({sensor.Valor}) incompatível com o sensor do drone ({drone.TipoSensor}).",
                sensor.Linha, sensor.Coluna, sensor.NomeOriginal);

        var gsd = missao.ObterPrimeiro("GSD");
        if (gsd is not null && gsd.TentaObterNumero(out var gsdValor) && gsdValor < drone.GsdMinimoCmPorPixel)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "DRN010",
                $"GSD solicitado ({gsdValor:F2} cm/pixel) exige mais detalhe do que o drone suporta ({drone.GsdMinimoCmPorPixel:F2} cm/pixel).",
                gsd.Linha, gsd.Coluna, gsd.NomeOriginal);

        var raio = missao.ObterPrimeiro("RAIO_MISSAO");
        if (raio is not null && raio.TentaObterNumero(out var raioValor) && raioValor * 2 > drone.AlcanceKm * 0.70)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "DRN011",
                $"A rota estimada ({raioValor * 2:F2} km) ultrapassa a distância segura de 70% do alcance do drone ({drone.AlcanceKm * 0.70:F2} km).",
                raio.Linha, raio.Coluna, raio.NomeOriginal);

        var anti = missao.ObterPrimeiro("DESVIO_OBSTACULOS");
        if ((anti?.Valor.ToUpperInvariant() is "ATIVO" or "SIM") && !drone.SistemaAntiColisao)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, "DRN012",
                "A missão exige anticolisão, mas o drone selecionado não possui esse sistema.",
                anti.Linha, anti.Coluna, anti.NomeOriginal);

        var terrain = missao.ObterPrimeiro("TERRAIN_FOLLOWING");
        if ((terrain?.Valor.ToUpperInvariant() is "ATIVO" or "SIM") && !drone.TerrainFollowing)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, "DRN013",
                "A missão exige Terrain Following, mas o drone selecionado não possui esse recurso.",
                terrain.Linha, terrain.Coluna, terrain.NomeOriginal);

        var rth = missao.ObterPrimeiro("RTH");
        if ((rth?.Valor.ToUpperInvariant() is "ATIVO" or "SIM") && !drone.Rth)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, "DRN014",
                "A missão exige retorno automático à base, mas o drone selecionado não possui RTH.",
                rth.Linha, rth.Coluna, rth.NomeOriginal);

        var bateria = missao.ObterPrimeiro("BATERIA");
        if (bateria is not null && bateria.TentaObterNumero(out var bateriaValor) &&
            Math.Abs(bateriaValor - drone.CapacidadeBateriaMah) > 0.01)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "DRN015",
                $"A bateria informada ({bateriaValor:F0} mAh) difere da capacidade cadastrada do drone ({drone.CapacidadeBateriaMah:F0} mAh). Será usada a capacidade informada para os cálculos da missão.",
                bateria.Linha, bateria.Coluna, bateria.NomeOriginal);
    }

    private static void CompararDroneNumerico(Missao missao, RelatorioMissao relatorio, string nome, double limite, string codigo, string mensagem)
    {
        var comando = missao.ObterPrimeiro(nome);
        if (comando is not null && comando.TentaObterNumero(out var valor) && valor > limite)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, codigo, mensagem,
                comando.Linha, comando.Coluna, comando.NomeOriginal);
    }

    private static void AvaliarMeteorologia(Missao missao, RelatorioMissao relatorio)
    {
        var vento = missao.ObterPrimeiro("VENTO");
        if (vento is not null && vento.TentaObterNumero(out var valor) && valor > 30)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Alerta,
                "MET001",
                $"Condição de vento acima do limite definido: {valor:F1} km/h > 30 km/h.",
                vento.Linha,
                vento.Coluna,
                vento.NomeOriginal);
        }

        var chuva = missao.ObterPrimeiro("CHUVA");
        if (chuva is not null && chuva.TentaObterNumero(out var chuvaMm) && chuvaMm > 0)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Critico,
                "MET002",
                $"Precipitação detectada ({chuvaMm:F1} mm/h): operação aérea suspensa no modelo acadêmico.",
                chuva.Linha,
                chuva.Coluna,
                chuva.NomeOriginal);
        }

        var nuvens = missao.ObterPrimeiro("NUVENS");
        if (nuvens is not null && nuvens.TentaObterNumero(out var cobertura) && cobertura > 70)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Alerta,
                "MET003",
                $"Cobertura de nuvens elevada ({cobertura:F1}%): sombras podem prejudicar capturas e índices de vegetação.",
                nuvens.Linha,
                nuvens.Coluna,
                nuvens.NomeOriginal);
        }

        var umidade = missao.ObterPrimeiro("UMIDADE");
        if (umidade is not null && umidade.TentaObterNumero(out var umidadeValor) && umidadeValor > 90)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Alerta,
                "MET004",
                $"Umidade elevada ({umidadeValor:F1}%): considerar risco de condensação e proteção dos sensores.",
                umidade.Linha,
                umidade.Coluna,
                umidade.NomeOriginal);
        }

        var iluminacao = missao.ObterPrimeiro("ILUMINACAO");
        if (iluminacao is not null && iluminacao.TentaObterNumero(out var luz) && luz < 30)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Alerta,
                "MET005",
                $"Luminosidade baixa ({luz:F1}%): a qualidade radiométrica das imagens pode ser reduzida.",
                iluminacao.Linha,
                iluminacao.Coluna,
                iluminacao.NomeOriginal);
        }
    }

    private static void AvaliarMapeamento(Missao missao, RelatorioMissao relatorio)
    {
        var gsd = missao.ObterPrimeiro("GSD");
        if (gsd is not null && gsd.TentaObterNumero(out var gsdValor))
        {
            relatorio.GsdCmPorPixel = gsdValor;
            if (gsdValor > 10)
            {
                relatorio.Adicionar(
                    "SEMÂNTICA",
                    SeveridadeDiagnostico.Alerta,
                    "MAP001",
                    $"GSD elevado ({gsdValor:F2} cm/pixel): a resolução pode ser insuficiente para detalhes agrícolas.",
                    gsd.Linha,
                    gsd.Coluna,
                    gsd.NomeOriginal);
            }
        }

        var frontal = ObterNumeroOpcional(missao.ObterPrimeiro("SOBREPOSICAO_FRONTAL"), relatorio);
        var lateral = ObterNumeroOpcional(missao.ObterPrimeiro("SOBREPOSICAO_LATERAL"), relatorio);
        if (frontal.HasValue && frontal.Value < 70)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "MAP002",
                $"Sobreposição frontal abaixo do recomendado: {frontal.Value:F1}%.", parametro: "SOBREPOSICAO_FRONTAL");
        if (lateral.HasValue && lateral.Value < 60)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "MAP003",
                $"Sobreposição lateral abaixo do recomendado: {lateral.Value:F1}%.", parametro: "SOBREPOSICAO_LATERAL");

        var velocidade = missao.ObterPrimeiro("VELOCIDADE");
        if (velocidade is not null && velocidade.TentaObterNumero(out var velocidadeValor) && velocidadeValor > 35)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "MAP004",
                $"Velocidade elevada ({velocidadeValor:F1} km/h): pode causar motion blur conforme a câmera e o tempo de exposição.",
                velocidade.Linha, velocidade.Coluna, velocidade.NomeOriginal);
    }

    private static void AvaliarOperacaoESensores(Missao missao, RelatorioMissao relatorio)
    {
        var sensor = missao.ObterPrimeiro("SENSOR");
        if (sensor is not null)
        {
            relatorio.SensorDetectado = sensor.Valor.ToUpperInvariant();
            var sensoresConhecidos = new[] { "RGB", "MULTIESPECTRAL", "HIPERESPECTRAL", "LIDAR" };
            if (!sensoresConhecidos.Contains(relatorio.SensorDetectado, StringComparer.OrdinalIgnoreCase))
                relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "SEN001",
                    $"Sensor '{sensor.Valor}' não possui regras específicas; será tratado como sensor genérico.",
                    sensor.Linha, sensor.Coluna, sensor.NomeOriginal);
            else
                relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Informacao, "SEN002",
                    $"Sensor selecionado: {sensor.Valor}. O projeto registra o tipo, mas não calcula NDVI, nuvem de pontos ou classificação agrícola.",
                    sensor.Linha, sensor.Coluna, sensor.NomeOriginal);
        }

        var operacao = missao.ObterPrimeiro("OPERACAO");
        if (operacao is not null)
        {
            relatorio.OperacaoDetectada = operacao.Valor.ToUpperInvariant();
            if (relatorio.OperacaoDetectada is not ("VLOS" or "BVLOS"))
                relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, "SEG001",
                    "OPERACAO deve ser VLOS ou BVLOS.", operacao.Linha, operacao.Coluna, operacao.NomeOriginal);
            else if (relatorio.OperacaoDetectada == "BVLOS")
                relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "SEG002",
                    "Operação BVLOS requer autorização e avaliação regulatória específica; o sistema não consulta órgãos externos.",
                    operacao.Linha, operacao.Coluna, operacao.NomeOriginal);
        }

        var terrain = missao.ObterPrimeiro("TERRAIN_FOLLOWING");
        relatorio.TerrainFollowingAtivo = terrain?.Valor.ToUpperInvariant() is "ATIVO" or "SIM";
        if (relatorio.Fazenda?.Relevo.Contains("MONTANHOSO", StringComparison.OrdinalIgnoreCase) == true ||
            relatorio.Fazenda?.Relevo.Contains("ÍNGREME", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (!relatorio.TerrainFollowingAtivo)
                relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "SEG003",
                    "Relevo acentuado sem TERRAIN_FOLLOWING ativo; a altitude relativa ao solo pode variar.", parametro: "TERRAIN_FOLLOWING");
            else
                relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Informacao, "SEG004",
                    "Terrain Following ativo para relevo acentuado.", parametro: "TERRAIN_FOLLOWING");
        }

        AvaliarRotaERth(missao, relatorio);
    }

    private static void AvaliarRotaERth(Missao missao, RelatorioMissao relatorio)
    {
        var raio = missao.ObterPrimeiro("RAIO_MISSAO");
        var velocidade = ObterNumeroOuPadrao(missao.ObterPrimeiro("VELOCIDADE"), 25);
        if (raio is not null && raio.TentaObterNumero(out var raioKm))
        {
            relatorio.DistanciaMissaoKm = raioKm * 2.0;
            relatorio.TempoVooMinutos = relatorio.DistanciaMissaoKm.Value / Math.Max(velocidade, 1) * 60.0;
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Informacao, "ROT001",
                $"Rota estimada de {relatorio.DistanciaMissaoKm.Value:F2} km e {relatorio.TempoVooMinutos.Value:F1} min, usando ida e retorno pelo raio informado.",
                raio.Linha, raio.Coluna, raio.NomeOriginal);
        }

        var homeLat = missao.ObterPrimeiro("HOME_LATITUDE");
        var homeLon = missao.ObterPrimeiro("HOME_LONGITUDE");
        if ((homeLat is null) != (homeLon is null))
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, "ROT002",
                "HOME_LATITUDE e HOME_LONGITUDE devem ser informados em conjunto.", parametro: "HOME_POINT");

        var rth = missao.ObterPrimeiro("RTH");
        if (raio is not null && rth is null)
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "ROT003",
                "RAIO_MISSAO informado sem RTH; informe RTH = ATIVO ou SIM para documentar o retorno automático.", parametro: "RTH");
        if (rth is not null && rth.Valor.ToUpperInvariant() is not ("ATIVO" or "INATIVO" or "SIM" or "NAO"))
            relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Erro, "ROT004",
                "RTH deve ter estado ATIVO, INATIVO, SIM ou NAO.", rth.Linha, rth.Coluna, rth.NomeOriginal);

        var reserva = missao.ObterPrimeiro("RESERVA_BATERIA");
        if (reserva is not null && reserva.TentaObterNumero(out var reservaMah))
        {
            relatorio.ReservaBateriaMah = reservaMah;
            if (relatorio.CapacidadeBateriaMah.HasValue && relatorio.ConsumoEstimadoMah.HasValue &&
                relatorio.CapacidadeBateriaMah.Value - relatorio.ConsumoEstimadoMah.Value < reservaMah)
                relatorio.Adicionar("SEMÂNTICA", SeveridadeDiagnostico.Alerta, "ROT005",
                    $"A margem de bateria ({relatorio.CapacidadeBateriaMah.Value - relatorio.ConsumoEstimadoMah.Value:F0} mAh) é menor que a reserva RTH ({reservaMah:F0} mAh).",
                    reserva.Linha, reserva.Coluna, reserva.NomeOriginal);
        }
    }

    private static void AvaliarRegulamentacao(Missao missao, RelatorioMissao relatorio)
    {
        var regulamento = missao.ObterPrimeiro("REGULAMENTACAO");
        if (regulamento is null)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Informacao,
                "REG001",
                "Regulamentação não informada; a missão não foi certificada por este campo.",
                parametro: "REGULAMENTACAO");
            return;
        }

        if (!string.Equals(regulamento.Valor, "SISANT_OK", StringComparison.OrdinalIgnoreCase))
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Erro,
                "REG002",
                "A regulamentação está pendente ou em estado não permitido; informe SISANT_OK.",
                regulamento.Linha,
                regulamento.Coluna,
                regulamento.NomeOriginal);
        }
    }

    private static void AvaliarCoberturaEAutonomia(Missao missao, RelatorioMissao relatorio)
    {
        var areaComando = missao.ObterPrimeiro("AREA");
        if (areaComando is null || !areaComando.TentaObterNumero(out var area))
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Informacao,
                "AUT001",
                "Área não informada; cobertura e autonomia não foram estimadas.",
                parametro: "AREA");
            return;
        }

        var sobreposicao = 0.0;
        var overlapComando = missao.ObterPrimeiro("SOBREPOSICAO");
        if (overlapComando is not null && overlapComando.TentaObterNumero(out var overlapValor))
            sobreposicao = overlapValor;

        var frontal = missao.ObterPrimeiro("SOBREPOSICAO_FRONTAL");
        var lateral = missao.ObterPrimeiro("SOBREPOSICAO_LATERAL");
        if (frontal is not null && lateral is not null &&
            frontal.TentaObterNumero(out var frontalValor) && lateral.TentaObterNumero(out var lateralValor))
            sobreposicao = (frontalValor + lateralValor) / 2.0;
        else if (frontal is not null && frontal.TentaObterNumero(out var apenasFrontal))
            sobreposicao = apenasFrontal;

        var fatorSobreposicao = 1.0 - sobreposicao / 100.0;
        if (fatorSobreposicao <= 0)
            return;

        var areaPlanejada = area / fatorSobreposicao;
        relatorio.CoberturaEstimadaHa = areaPlanejada;
        relatorio.EficienciaCoberturaPercentual = 100.0 / (1.0 + sobreposicao / 100.0);

        var altitude = ObterNumeroOuPadrao(missao.ObterPrimeiro("ALTITUDE"), 50);
        var velocidade = ObterNumeroOuPadrao(missao.ObterPrimeiro("VELOCIDADE"), 25);
        var vento = ObterNumeroOuPadrao(missao.ObterPrimeiro("VENTO"), 0);
        var baseMah = 300.0 + areaPlanejada * 250.0;
        var fatorAltitude = 1.0 + Math.Max(0, altitude - 50.0) / 500.0;
        var fatorVelocidade = 1.0 + Math.Abs(velocidade - 25.0) / 100.0;
        var fatorVento = 1.0 + Math.Max(0, vento - 10.0) / 100.0;
        var consumo = baseMah * fatorAltitude * fatorVelocidade * fatorVento;

        var anti = missao.ObterPrimeiro("DESVIO_OBSTACULOS")?.Valor.ToUpperInvariant();
        if (anti is "ATIVO" or "SIM" &&
            relatorio.AlturaMaiorObstaculoM.HasValue && altitude <= relatorio.AlturaMaiorObstaculoM.Value)
            consumo += 500.0;

        relatorio.ConsumoEstimadoMah = consumo;
        relatorio.MemoriaRequeridaMb = 128.0 + areaPlanejada * 8.0 +
            (missao.ObterPrimeiro("SENSOR") is null ? 0 : 64);

        var bateriaComando = missao.ObterPrimeiro("BATERIA");
        if (bateriaComando is null || !bateriaComando.TentaObterNumero(out var bateria))
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Informacao,
                "AUT002",
                $"Consumo estimado: {consumo:F0} mAh; capacidade de bateria não informada para comparação.",
                parametro: "BATERIA");
            return;
        }

        relatorio.CapacidadeBateriaMah = bateria;
        if (consumo > bateria)
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Alerta,
                "AUT003",
                $"Autonomia insuficiente: consumo estimado de {consumo:F0} mAh supera a bateria de {bateria:F0} mAh.",
                bateriaComando.Linha,
                bateriaComando.Coluna,
                bateriaComando.NomeOriginal);
        }
        else
        {
            relatorio.Adicionar(
                "SEMÂNTICA",
                SeveridadeDiagnostico.Informacao,
                "AUT004",
                $"Autonomia compatível: consumo estimado de {consumo:F0} mAh para bateria de {bateria:F0} mAh.",
                bateriaComando.Linha,
                bateriaComando.Coluna,
                bateriaComando.NomeOriginal);
        }
    }

    private static double ObterNumeroOuPadrao(ComandoMissao? comando, double padrao) =>
        comando is not null && comando.TentaObterNumero(out var valor) ? valor : padrao;

    private static void DefinirNivelRisco(RelatorioMissao relatorio)
    {
        if (relatorio.TemCritico)
        {
            relatorio.NivelRisco = "CRÍTICO";
            return;
        }

        var temErro = relatorio.Diagnosticos.Any(d => d.Severidade == SeveridadeDiagnostico.Erro);
        var alertaAutonomia = relatorio.Diagnosticos.Any(d => d.Codigo == "AUT003");
        var alertaVento = relatorio.Diagnosticos.Any(d => d.Codigo == "MET001");
        if (temErro || alertaAutonomia || alertaVento)
        {
            relatorio.NivelRisco = "ALTO";
            return;
        }

        if (relatorio.Diagnosticos.Any(d => d.Severidade == SeveridadeDiagnostico.Alerta))
        {
            relatorio.NivelRisco = "MÉDIO";
            return;
        }

        relatorio.NivelRisco = "BAIXO";
    }
}