using System.Diagnostics;
using System.Runtime.InteropServices;

// Uso:
//   stock-quote-alert <ATIVO> <PRECO_VENDA> <PRECO_COMPRA>   → salva o ativo e garante que o monitor está rodando
//   stock-quote-alert monitor                                → modo interno: o loop que fica checando as cotações



// monitor
// Tratado primeiro, para que o monitor nunca tente criar outro monitor.
if (args.Length == 1 && args[0] == "monitor")
{
    // "kill <pid>" (SIGTERM) e Ctrl+C (SIGINT) encerram o processo sem passar pelo finally,
    // então o arquivo de PID também é apagado quando um desses sinais chega.
    using var aoTerminar    = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => File.Delete("monitor.pid"));
    using var aoInterromper = PosixSignalRegistration.Create(PosixSignal.SIGINT,  _ => File.Delete("monitor.pid"));

    try
    {
        await Monitor.RodarAsync();
    }
    finally
    {
        File.Delete("monitor.pid");
    }
    return;
}
else // modo prinicipal (cadastro)
{
    // Lê os argumentos da linha de comando
    var ativo       = args[0].ToUpperInvariant();   // ex: "PETR4"
    var precoVenda  = decimal.Parse(args[1]);
    var precoCompra = decimal.Parse(args[2]);

    // Insere o ativo no banco; se ele já existir, atualiza os preços de referência
    using var db = Banco.Abrir();

    var cmd = db.CreateCommand();
    cmd.CommandText = """
        INSERT INTO alertas (ativo, preco_venda, preco_compra)
        VALUES ($ativo, $venda, $compra)
        ON CONFLICT(ativo) DO UPDATE SET preco_venda = $venda, preco_compra = $compra
        """;
    cmd.Parameters.AddWithValue("$ativo",  ativo);
    cmd.Parameters.AddWithValue("$venda",  precoVenda);
    cmd.Parameters.AddWithValue("$compra", precoCompra);
    cmd.ExecuteNonQuery();

    // Se não há um monitor rodando, inicia um em segundo plano (este mesmo programa, em modo "monitor")
    if (!MonitorRodando())
    {
        var monitor = new ProcessStartInfo
        {
            FileName        = Environment.ProcessPath!,
            Arguments       = "monitor",
            UseShellExecute = false,
        };

        var processo = Process.Start(monitor)!;
        File.WriteAllText("monitor.pid", $"{processo.Id}");
    }
}


// Funcoes Auxiliares

// O arquivo de PID pode ter sobrado de um monitor que morreu (kill -9, reboot),
// então confere se o processo daquele PID existe mesmo e se é este programa.
bool MonitorRodando()
{
    if (!File.Exists("monitor.pid")) return false;
    if (!int.TryParse(File.ReadAllText("monitor.pid"), out var pid)) return false;

    try
    {
        var p = Process.GetProcessById(pid);   // lança exceção se o processo não existe
        return !p.HasExited && p.ProcessName == Process.GetCurrentProcess().ProcessName;
    }
    catch (ArgumentException)
    {
        return false;
    }
}


// ─── Tipos de dados ─────────────────────────────────────────────────────────

// Configuração lida do appsettings.json
record Config(string EmailDestino, SmtpConfig Smtp);
record SmtpConfig(string Host, int Porta, string Usuario, string Senha);

// Um ativo monitorado, como está salvo no banco
record Alerta(string Ativo, double PrecoVenda, double PrecoCompra);

// Resposta da busca de tickers da brapi (/api/v2/tickers)
record TickerListResponse(List<TickerItem> Results);
record TickerItem(string Symbol, string Name, TickerQuote Quote);
record TickerQuote(double? LastPrice, double? ChangePercent, double? Volume, double? MarketCap);

// Resposta da cotação da brapi (/api/v2/stocks/quote)
record QuoteResponse(List<QuoteResult> Results);
record QuoteResult(string Symbol, QuoteData Data);
record QuoteData(
    double? RegularMarketPrice,
    double? RegularMarketChangePercent,
    double? RegularMarketDayLow,
    double? RegularMarketDayHigh,
    double? RegularMarketVolume);
