using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

// Loop que roda em segundo plano: a cada minuto lê os ativos do banco,
// busca as cotações e manda um e-mail quando o preço sai da faixa.
static class Monitor
{
    public static async Task RodarAsync()
    {
        // Setup

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        // Tudo que for escrito no console vai para o arquivo de log
        var log = new StreamWriter("monitor.log", append: true) { AutoFlush = true };
        Console.SetOut(log);
        Console.SetError(log);

        // Configuração de e-mail/SMTP
        var opcoes  = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var caminho = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var config  = JsonSerializer.Deserialize<Config>(File.ReadAllText(caminho), opcoes)!;

        // Cliente HTTP da brapi, autenticado com o token
        var token = Environment.GetEnvironmentVariable("BRAPI_TOKEN")   // definido no terminal
            ?? throw new InvalidOperationException("Defina a variável BRAPI_TOKEN");

        using var http = new HttpClient { BaseAddress = new Uri("https://brapi.dev") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Loop principal 

        do
        {
            try
            {
                var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // 1) Lê do banco os ativos cadastrados
                var alertas = new List<Alerta>();

                using (var db = Banco.Abrir())
                {
                    var cmd = db.CreateCommand();
                    cmd.CommandText = "SELECT ativo, preco_venda, preco_compra FROM alertas";

                    using var leitor = cmd.ExecuteReader();
                    while (leitor.Read())                      // avança linha a linha; false quando acaba
                    {
                        alertas.Add(new Alerta(
                            leitor.GetString(0),               // coluna 0: ativo
                            leitor.GetDouble(1),               // coluna 1: preco_venda
                            leitor.GetDouble(2)));             // coluna 2: preco_compra
                    }
                }

                // 2) Busca a cotação de todos os ativos em um único request
                var simbolos = string.Join(",", alertas.Select(a => a.Ativo));
                var cotacao  = JsonSerializer.Deserialize<QuoteResponse>(
                    await http.GetStringAsync($"/api/v2/stocks/quote?symbols={simbolos}"), json)!;

                // 3) Compara cada preço com a faixa do ativo e avisa se saiu dela
                for (int i = 0; i < cotacao.Results.Count; i++)
                {
                    var resultado = cotacao.Results[i];
                    var simbolo   = resultado.Symbol;
                    var agora     = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    if (resultado.Data.RegularMarketPrice is not double preco)
                    {
                        Console.WriteLine($"[{agora}] {simbolo}: cotação indisponível");
                        continue;
                    }

                    var alerta = alertas.FirstOrDefault(a =>
                        string.Equals(a.Ativo, simbolo, StringComparison.OrdinalIgnoreCase));
                    if (alerta is null)
                        continue;

                    if (preco > alerta.PrecoVenda)
                    {
                        Console.WriteLine($"[{agora}] ALERTA {simbolo}: VENDER {simbolo} está em R$ {preco:F2}, acima de R$ {alerta.PrecoVenda:F2}.");
                        await EnviarEmailAsync(config,
                            $"Vender {simbolo}",
                            $"{simbolo} está em R$ {preco:F2}, acima de R$ {alerta.PrecoVenda:F2}.");
                    }
                    else if (preco < alerta.PrecoCompra)
                    {
                        Console.WriteLine($"[{agora}] ALERTA {simbolo}: VENDER {simbolo} está em R$ {preco:F2}, abaixo de R$ {alerta.PrecoCompra:F2}.");
                        await EnviarEmailAsync(config,
                            $"Vender {simbolo}",
                            $"{simbolo} está em R$ {preco:F2}, Abaixo de R$ {alerta.PrecoCompra:F2}.");
                    }
                }
            }
            catch (Exception e)
            {
                // Registra o erro no log e tenta de novo no próximo minuto
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERRO: {e}");
            }
        }
        while (await timer.WaitForNextTickAsync());
    }

    // Envia um e-mail de texto simples usando o servidor SMTP do appsettings.json
    static async Task EnviarEmailAsync(Config config, string assunto, string corpo)
    {
        // Monta a mensagem
        var mensagem = new MimeMessage();
        mensagem.From.Add(MailboxAddress.Parse(config.Smtp.Usuario));
        mensagem.To.Add(MailboxAddress.Parse(config.EmailDestino));
        mensagem.Subject = assunto;
        mensagem.Body    = new TextPart("plain") { Text = corpo };

        // Conecta, faz login, envia e desconecta
        using var smtp = new SmtpClient();
        smtp.CheckCertificateRevocation = false;   // a consulta de revogação falha nesta rede; o resto da validação continua
        await smtp.ConnectAsync(config.Smtp.Host, config.Smtp.Porta, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(config.Smtp.Usuario, config.Smtp.Senha);
        await smtp.SendAsync(new SendRequest(mensagem));   // MailKit 5: a mensagem vai dentro de um SendRequest
        await smtp.DisconnectAsync(true);
    }
}
