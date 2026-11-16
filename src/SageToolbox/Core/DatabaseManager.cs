using System.Data;
using Microsoft.Data.SqlClient;

namespace SageToolbox.Core;

public enum ConnectionMode
{
    /// <summary>Sage 100 sur SQL Server : une base par dossier, authentification Windows, schéma dbo.</summary>
    OnPremise,

    /// <summary>Sage Premium Cloud (SPC) : serveur Azure SQL, authentification SQL, une société par schéma.</summary>
    Spc
}

/// <summary>Société d'un dossier SPC (table cbase.companies) : <see cref="SchemaName"/> préfixe ses tables Sage.</summary>
public sealed record SpcCompany(string Name, string SchemaName)
{
    public override string ToString() => Name;
}

/// <summary>
/// Accès SQL Server en authentification Windows intégrée (Integrated Security=true),
/// via le driver natif Microsoft.Data.SqlClient. Remplace la logique de sélection du
/// meilleur driver ODBC (18 -> 17 -> 13 -> Native Client 11 -> "SQL Server") de la V1/V2 :
/// avec un driver .NET natif, cette détection n'a plus lieu d'être.
/// </summary>
public sealed class DatabaseManager
{
    private static readonly Lazy<DatabaseManager> LazyInstance = new(() => new DatabaseManager());
    public static DatabaseManager Instance => LazyInstance.Value;

    private SqlConnectionStringBuilder? _spcBuilder;

    public ConnectionMode Mode { get; private set; } = ConnectionMode.OnPremise;
    public bool IsSpc => Mode == ConnectionMode.Spc;

    /// <summary>Serveur SQL (en SPC : serveur Azure).</summary>
    public string? CurrentInstance { get; private set; }

    /// <summary>Base Sage de travail (en SPC : nom de la société sélectionnée).</summary>
    public string? CurrentDatabase { get; private set; }

    /// <summary>Schéma SQL de la société sélectionnée en SPC ; null hors SPC.</summary>
    public string? CurrentSchema { get; private set; }

    /// <summary>Schéma qui préfixe les tables Sage : celui de la société en SPC, dbo sinon.</summary>
    public string SchemaName => CurrentSchema ?? "dbo";

    /// <summary>Base SQL physique à laquelle on se connecte en SPC (la société est un schéma de cette base).</summary>
    public string? SpcDatabase => _spcBuilder?.InitialCatalog;

    /// <summary>Sociétés du dossier SPC lues à la connexion (cbase.companies).</summary>
    public IReadOnlyList<SpcCompany> SpcCompanies { get; private set; } = [];

    public bool IsConnected => CurrentInstance is not null && CurrentDatabase is not null;

    private DatabaseManager()
    {
    }

    public static SqlConnectionStringBuilder BuildSpcConnectionString(string server, string database, string user, string password) =>
        new()
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = user,
            Password = password,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30,
            ApplicationName = "Sage Toolbox"
        };

    /// <summary>
    /// Connexion SPC : ouvre le serveur Azure SQL, lit les sociétés (cbase.companies) et passe en mode SPC.
    /// Aucune société n'est sélectionnée à l'issue : voir <see cref="SelectSpcCompany"/>. En cas d'échec l'état
    /// courant est conservé et l'exception est propagée.
    /// </summary>
    public async Task<List<SpcCompany>> ConnectSpcAsync(SqlConnectionStringBuilder builder)
    {
        var companies = new List<SpcCompany>();
        await using (var conn = new SqlConnection(builder.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT name, schemaName FROM cbase.companies ORDER BY name", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                companies.Add(new SpcCompany(reader.GetString(0).Trim(), reader.GetString(1).Trim()));
        }

        Mode = ConnectionMode.Spc;
        _spcBuilder = builder;
        CurrentInstance = builder.DataSource;
        CurrentDatabase = null;
        CurrentSchema = null;
        SpcCompanies = companies;
        return companies;
    }

    /// <summary>Sélectionne la société SPC de travail : toutes les tables Sage sont alors préfixées par son schéma.</summary>
    public void SelectSpcCompany(SpcCompany company)
    {
        if (!IsSpc) throw new InvalidOperationException("Aucune connexion SPC active.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(company.SchemaName, "^[A-Za-z0-9_]+$"))
            throw new ArgumentException($"Nom de schéma invalide : '{company.SchemaName}'", nameof(company));

        CurrentDatabase = company.Name;
        CurrentSchema = company.SchemaName;
    }

    private static string BuildConnectionString(string instance, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = instance,
            InitialCatalog = database,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = false,
            ConnectTimeout = 10,
            ApplicationName = "Sage Toolbox"
        };
        return builder.ConnectionString;
    }

    public async Task<(bool Success, string? Error)> TestConnectionAsync(string instance, string database)
    {
        try
        {
            await using var conn = new SqlConnection(BuildConnectionString(instance, database));
            await conn.OpenAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public void Connect(string instance, string database)
    {
        ResetSpc();
        CurrentInstance = instance;
        CurrentDatabase = database;
    }

    /// <summary>Connexion au serveur seul, sans base sélectionnée — pour les opérations
    /// "Serveur &amp; Instance" qui ne nécessitent pas de base précise.</summary>
    public void ConnectInstanceOnly(string instance)
    {
        ResetSpc();
        CurrentInstance = instance;
        CurrentDatabase = null;
    }

    public void Disconnect()
    {
        ResetSpc();
        CurrentInstance = null;
        CurrentDatabase = null;
    }

    private void ResetSpc()
    {
        Mode = ConnectionMode.OnPremise;
        _spcBuilder = null;
        CurrentSchema = null;
        SpcCompanies = [];
    }

    public SqlConnection CreateConnection(string? instanceOverride = null, string? databaseOverride = null)
    {
        // SPC : une seule base Azure, jamais de bascule vers master ou une autre base.
        if (IsSpc) return new SqlConnection(_spcBuilder!.ConnectionString);

        var instance = instanceOverride ?? CurrentInstance
            ?? throw new InvalidOperationException("Aucune connexion active.");
        var database = databaseOverride ?? CurrentDatabase
            ?? throw new InvalidOperationException("Aucune base sélectionnée.");
        return new SqlConnection(BuildConnectionString(instance, database));
    }

    /// <summary>
    /// Substitution du placeholder de schéma ({schema}) par le schéma courant : [dbo] pour Sage 100 SQL Server,
    /// le schéma de la société sélectionnée en SPC. Tout SQL qui nomme une table Sage doit l'utiliser
    /// ({schema}.F_COMPTET) et passer par cette méthode lorsqu'il crée lui-même sa commande.
    /// </summary>
    public string ResolveSchema(string sql) => sql.Replace("{schema}", QuoteIdentifier(SchemaName));

    public async Task<DataTable> ExecuteQueryAsync(
        string sql,
        IDictionary<string, object?>? parameters = null,
        string? databaseOverride = null)
    {
        await using var conn = CreateConnection(databaseOverride: databaseOverride);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(ResolveSchema(sql), conn);
        AddParameters(cmd, parameters);
        await using var reader = await cmd.ExecuteReaderAsync();
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    public async Task<object?> ExecuteScalarAsync(
        string sql,
        IDictionary<string, object?>? parameters = null,
        string? databaseOverride = null)
    {
        await using var conn = CreateConnection(databaseOverride: databaseOverride);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(ResolveSchema(sql), conn);
        AddParameters(cmd, parameters);
        return await cmd.ExecuteScalarAsync();
    }

    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        IDictionary<string, object?>? parameters = null,
        string? databaseOverride = null)
    {
        await using var conn = CreateConnection(databaseOverride: databaseOverride);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(ResolveSchema(sql), conn);
        AddParameters(cmd, parameters);
        return await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Connexion "autocommit" dédiée pour DDL/BACKUP (opérations qui ne peuvent pas
    /// être englobées dans une transaction utilisateur sous SQL Server).
    /// </summary>
    public async Task<SqlConnection> OpenAutocommitConnectionAsync(string? instanceOverride = null, string? databaseOverride = null)
    {
        var conn = CreateConnection(instanceOverride, databaseOverride);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Exécute une séquence de requêtes liées dans une transaction atomique
    /// (rollback global en cas d'échec partiel).
    /// </summary>
    public async Task RunInTransactionAsync(Func<SqlConnection, SqlTransaction, Task> work, string? databaseOverride = null)
    {
        await using var conn = CreateConnection(databaseOverride: databaseOverride);
        await conn.OpenAsync();
        var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            await work(conn, tx);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        finally
        {
            await tx.DisposeAsync();
        }
    }

    public static async Task<int> ExecuteInTransactionAsync(
        SqlConnection conn,
        SqlTransaction tx,
        string sql,
        IDictionary<string, object?>? parameters = null)
    {
        await using var cmd = new SqlCommand(Instance.ResolveSchema(sql), conn, tx);
        AddParameters(cmd, parameters);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParameters(SqlCommand cmd, IDictionary<string, object?>? parameters)
    {
        if (parameters is null) return;
        foreach (var (key, value) in parameters)
            cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
    }

    /// <summary>
    /// Échappement d'identifiant SQL Server ([nom] avec doublement des ']').
    /// Réservé aux identifiants provenant de listes fermées ou validés par regex
    /// (pas de saisie libre directement interpolée) — cf. point d'architecture #2.
    /// </summary>
    public static string QuoteIdentifier(string identifier) => "[" + identifier.Replace("]", "]]") + "]";

    public async Task<bool> ObjectExistsAsync(string tableName, string? databaseOverride = null)
    {
        var result = await ExecuteScalarAsync(
            "SELECT OBJECT_ID(@name)",
            new Dictionary<string, object?> { ["@name"] = SchemaName + "." + tableName },
            databaseOverride);
        return result is not null && result is not DBNull;
    }

    public async Task<bool> ColumnExistsAsync(string tableName, string columnName, string? databaseOverride = null)
    {
        const string sql = @"
            SELECT COUNT(*) FROM sys.columns c
            JOIN sys.tables t ON c.object_id = t.object_id
            WHERE t.name = @t AND c.name = @c AND t.schema_id = SCHEMA_ID(@schema)";
        var result = await ExecuteScalarAsync(sql, new Dictionary<string, object?>
        {
            ["@t"] = tableName,
            ["@c"] = columnName,
            ["@schema"] = SchemaName
        }, databaseOverride);
        return Convert.ToInt32(result) > 0;
    }
}
