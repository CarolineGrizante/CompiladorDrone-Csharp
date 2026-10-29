using System.IO;
using System.Text;
using System.Windows;
using CompiladorDrone.Modelos;
using CompiladorDrone.Pipeline;
using Microsoft.Win32;

namespace CompiladorDrone;

public partial class MainWindow : Window
{
    private RelatorioMissao? _ultimoRelatorio;

    public MainWindow()
    {
        InitializeComponent();
        var exemploInicial = ResolverCaminho(Path.Combine("Exemplos", "missao_sucesso.txt"));
        if (File.Exists(exemploInicial))
        {
            EditorTextBox.Text = File.ReadAllText(exemploInicial);
            ExecutarPipeline();
        }
    }

    private void AbrirArquivo_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Filter = "Arquivos de missão (*.txt)|*.txt|Todos os arquivos (*.*)|*.*",
            Title = "Abrir missão DSL"
        };
        if (dialogo.ShowDialog() == true)
        {
            EditorTextBox.Text = File.ReadAllText(dialogo.FileName);
            ExecutarPipeline();
        }
    }

    private void Executar_Click(object sender, RoutedEventArgs e) => ExecutarPipeline();
    private void Sucesso_Click(object sender, RoutedEventArgs e) => CarregarExemplo("missao_sucesso.txt");
    private void Sinonimos_Click(object sender, RoutedEventArgs e) => CarregarExemplo("missao_sinonimos.txt");
    private void ErroLexico_Click(object sender, RoutedEventArgs e) => CarregarExemplo("missao_erro_lexico.txt");
    private void ErroSintatico_Click(object sender, RoutedEventArgs e) => CarregarExemplo("missao_erro_sintatico.txt");
    private void Risco_Click(object sender, RoutedEventArgs e) => CarregarExemplo("missao_risco.txt");
    private void Fatores_Click(object sender, RoutedEventArgs e) => CarregarExemplo("11_missao_completa_fatores.txt");

    private void CarregarExemplo(string nomeArquivo)
    {
        var caminho = ResolverCaminho(Path.Combine("Exemplos", nomeArquivo));
        if (!File.Exists(caminho))
        {
            MessageBox.Show($"Exemplo não encontrado: {caminho}", "Compilador de Missões", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        EditorTextBox.Text = File.ReadAllText(caminho);
        ExecutarPipeline();
    }

    private void ExecutarPipeline()
    {
        var relatorio = new RelatorioMissao();
        try
        {
            var lexer = new Lexer();
            var resultadoLexico = lexer.Analisar(EditorTextBox.Text);
            relatorio.Tokens.AddRange(resultadoLexico.Tokens);

            if (resultadoLexico.Diagnosticos.Count > 0)
            {
                relatorio.Diagnosticos.AddRange(resultadoLexico.Diagnosticos);
                relatorio.NivelRisco = "NÃO CALCULADO";
                AtualizarTela(relatorio, resultadoLexico.Tokens, "A análise foi interrompida após erro(s) léxico(s).");
                return;
            }

            var parser = new Parser();
            var resultadoSintatico = parser.Analisar(resultadoLexico.Tokens);
            if (resultadoSintatico.Diagnosticos.Count > 0)
            {
                relatorio.Diagnosticos.AddRange(resultadoSintatico.Diagnosticos);
                relatorio.Simbolos.AddRange(resultadoSintatico.Missao.Comandos.Select(Simbolo.APartirDe));
                relatorio.NivelRisco = "NÃO CALCULADO";
                AtualizarTela(relatorio, resultadoLexico.Tokens, "A análise foi interrompida após erro(s) sintático(s).");
                return;
            }

            // Os símbolos ficam disponíveis mesmo se houver uma falha externa
            // ao carregar a base geográfica.
            relatorio.Simbolos.AddRange(resultadoSintatico.Missao.Comandos.Select(Simbolo.APartirDe));

            var caminhoJson = ResolverCaminho(Path.Combine("Dados", "fazendas.json"));
            var geoGis = new ServicoGeoGIS(caminhoJson);
            var caminhoDrones = ResolverCaminho(Path.Combine("Dados", "drones.json"));
            var drones = new ServicoDrones(caminhoDrones);
            var semantico = new SemanticAnalyzer(geoGis, drones);
            relatorio = semantico.Analisar(resultadoSintatico.Missao, resultadoLexico.Tokens);
            AtualizarTela(relatorio, resultadoLexico.Tokens, "Pipeline concluído: léxico → sintático LL(1) → semântico.");
        }
        catch (Exception ex)
        {
            relatorio.Adicionar(
                "APLICAÇÃO",
                SeveridadeDiagnostico.Critico,
                "APP001",
                $"Falha ao executar o pipeline: {ex.Message}");
            relatorio.NivelRisco = "NÃO CALCULADO";
            AtualizarTela(relatorio, relatorio.Tokens, "A execução terminou com uma falha de infraestrutura.");
        }
    }

    private void AtualizarTela(RelatorioMissao relatorio, IReadOnlyList<Token> tokens, string cabecalho)
    {
        _ultimoRelatorio = relatorio;
        SimbolosDataGrid.ItemsSource = null;
        SimbolosDataGrid.ItemsSource = relatorio.Simbolos;
        SimbolosDetalhesDataGrid.ItemsSource = null;
        SimbolosDetalhesDataGrid.ItemsSource = relatorio.Simbolos;

        var textoTokens = new StringBuilder();
        foreach (var token in tokens)
            textoTokens.AppendLine(token.ToString());
        TokensTextBox.Text = textoTokens.ToString();

        var console = new StringBuilder();
        console.AppendLine(cabecalho);
        console.AppendLine(new string('=', 78));
        console.AppendLine($"Tokens reconhecidos: {tokens.Count(t => t.Tipo != TokenType.TK_FIM_ARQUIVO)}");
        console.AppendLine($"Símbolos reconhecidos: {relatorio.Simbolos.Count}");
        console.AppendLine();
        console.AppendLine("Use as abas para consultar informações e diagnósticos detalhados.");
        ConsoleTextBox.Text = console.ToString();

        var informacoes = relatorio.Diagnosticos
            .Where(d => d.Severidade == SeveridadeDiagnostico.Informacao)
            .ToList();
        InformacoesTextBox.Text = informacoes.Count == 0
            ? "Nenhuma informação adicional foi gerada."
            : MontarDiagnosticos(informacoes, "INFORMAÇÕES DA ANÁLISE");

        var alertasErros = relatorio.Diagnosticos
            .Where(d => d.Severidade != SeveridadeDiagnostico.Informacao)
            .ToList();
        AlertasErrosTextBox.Text = alertasErros.Count == 0
            ? "Nenhum alerta ou erro. A missão não apresentou problemas nesta fase."
            : MontarDiagnosticos(alertasErros, "ALERTAS E ERROS");
        AtualizarIndicadores(relatorio);
    }

    private static string MontarDiagnosticos(IReadOnlyList<Diagnostico> diagnosticos, string titulo)
    {
        var texto = new StringBuilder();
        texto.AppendLine(titulo);
        texto.AppendLine(new string('=', 78));
        foreach (var diagnostico in diagnosticos)
            texto.AppendLine(diagnostico.ToString());
        return texto.ToString();
    }

    private void AtualizarIndicadores(RelatorioMissao relatorio)
    {
        RiscoTextBlock.Text = relatorio.NivelRisco;
        FazendaTextBlock.Text = relatorio.Fazenda is null
            ? "—"
            : $"{relatorio.Fazenda.Id} — {relatorio.Fazenda.Nome} ({relatorio.Fazenda.Relevo})";
        ObstaculoTextBlock.Text = relatorio.AlturaMaiorObstaculoM.HasValue
            ? $"{relatorio.AlturaMaiorObstaculoM.Value:F1} m"
            : "—";
        ConsumoTextBlock.Text = relatorio.ConsumoEstimadoMah.HasValue
            ? $"{relatorio.ConsumoEstimadoMah.Value:F0} mAh"
            : "—";
        BateriaTextBlock.Text = relatorio.CapacidadeBateriaMah.HasValue
            ? $"{relatorio.CapacidadeBateriaMah.Value:F0} mAh"
            : "—";
        CoberturaTextBlock.Text = relatorio.CoberturaEstimadaHa.HasValue
            ? $"{relatorio.CoberturaEstimadaHa.Value:F2} ha ({relatorio.EficienciaCoberturaPercentual:F1}% eficiência)"
            : "—";
        MemoriaTextBlock.Text = relatorio.MemoriaRequeridaMb.HasValue
            ? $"{relatorio.MemoriaRequeridaMb.Value:F1} MB"
            : "—";
        GsdTextBlock.Text = relatorio.GsdCmPorPixel.HasValue
            ? $"{relatorio.GsdCmPorPixel.Value:F2} cm/pixel"
            : "—";
        DistanciaTextBlock.Text = relatorio.DistanciaMissaoKm.HasValue
            ? $"{relatorio.DistanciaMissaoKm.Value:F2} km"
            : "—";
        TempoVooTextBlock.Text = relatorio.TempoVooMinutos.HasValue
            ? $"{relatorio.TempoVooMinutos.Value:F1} min"
            : "—";
        SensorTextBlock.Text = relatorio.SensorDetectado;
        OperacaoTextBlock.Text = relatorio.OperacaoDetectada;
        TerrainTextBlock.Text = relatorio.TerrainFollowingAtivo ? "ATIVO" : "INATIVO / NÃO INFORMADO";
        DroneTextBlock.Text = relatorio.Drone is null
            ? "NÃO INFORMADO"
            : $"{relatorio.Drone.Id} — {relatorio.Drone.Modelo} ({relatorio.Drone.TipoSensor})";
        VelocidadeMaxDroneTextBlock.Text = relatorio.VelocidadeMaximaDroneKmh.HasValue
            ? $"{relatorio.VelocidadeMaximaDroneKmh.Value:F1} km/h"
            : "—";
        TetoDroneTextBlock.Text = relatorio.TetoVooDroneM.HasValue
            ? $"{relatorio.TetoVooDroneM.Value:F1} m"
            : "—";
        ResistenciaVentoDroneTextBlock.Text = relatorio.ResistenciaVentoDroneKmh.HasValue
            ? $"{relatorio.ResistenciaVentoDroneKmh.Value:F1} km/h"
            : "—";
        AlcanceDroneTextBlock.Text = relatorio.AlcanceDroneKm.HasValue
            ? $"{relatorio.AlcanceDroneKm.Value:F1} km (seguro: {relatorio.AlcanceDroneKm.Value * 0.70:F1} km)"
            : "—";
        ProtecaoUmidadeDroneTextBlock.Text = relatorio.ProtecaoUmidadeDronePercentual.HasValue
            ? $"{relatorio.ProtecaoUmidadeDronePercentual.Value:F1}%"
            : "—";
    }

    private static string ResolverCaminho(string relativo)
    {
        var candidatos = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, relativo),
            Path.Combine(Directory.GetCurrentDirectory(), relativo)
        };

        // Procura também nos diretórios-pai. Isso cobre execução pelo
        // Visual Studio, bin\Debug, bin\Release e pastas de publicação.
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        for (var nivel = 0; nivel < 10 && diretorio is not null; nivel++)
        {
            candidatos.Add(Path.Combine(diretorio.FullName, relativo));
            diretorio = diretorio.Parent;
        }

        var encontrado = candidatos
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
        return encontrado ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativo));
    }
}