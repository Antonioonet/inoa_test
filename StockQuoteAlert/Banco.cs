using Microsoft.Data.Sqlite;

// Acesso ao banco SQLite onde ficam os ativos monitorados
static class Banco
{
    // O arquivo do banco fica na mesma pasta do executável
    static readonly string Caminho = Path.Combine(AppContext.BaseDirectory, "alertas.db");

    // Abre a conexão e cria a tabela na primeira vez
    public static SqliteConnection Abrir()
    {
        var conexao = new SqliteConnection($"Data Source={Caminho}");
        conexao.Open();

        var cmd = conexao.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS alertas (
                ativo        TEXT PRIMARY KEY,
                preco_venda  REAL NOT NULL,
                preco_compra REAL NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();

        return conexao;
    }
}
