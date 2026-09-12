using System.Data;
using Microsoft.Data.SqlClient;

namespace SageToolbox.Core;

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

    public string? CurrentInstance { get; private set; }
    public string? CurrentDatabase { get; private set; }
    public bool IsConnected => CurrentInstance is not null && CurrentDatabase is not null;

    private DatabaseManager()
    {
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
        CurrentInstance = instance;
        CurrentDatabase = database;
    }

    /// <summary>Connexion au serveur seul, sans base sélectionnée — pour les opérations
    /// "Serveur &amp; Instance" qui ne nécessitent pas de base précise.</summary>
    public void ConnectInstanceOnly(string instance)
    {
        CurrentInstance = instance;
        CurrentDatabase = null;
    }

    public void Disconnect()
    {
        CurrentInstance = null;
        CurrentDatabase = null;
    }

    public SqlConnection CreateConnection(string? instanceOverride = null, string? databaseOverride = null)
    {
        var instance = instanceOverride ?? CurrentInstance
            ?? throw new InvalidOperationException("Aucune connexion active.");
        var database = databaseOverride ?? CurrentDatabase
            ?? throw new InvalidOperationException("Aucune base sélectionnée.");
        return new SqlConnection(BuildConnectionString(instance, database));
    }

    /// <summary>
    /// Substitution du placeholder de schéma ({schema} -> [dbo]), conservée pour
    /// parité avec la V1/V2 (schéma Sage toujours dbo en pratique, mais centralisé ici).
    /// </summary>
    private static string ResolveSchema(string sql) => sql.Replace("{schema}", "[dbo]");

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
        await using var cmd = new SqlCommand(ResolveSchema(sql), conn, tx);
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
            new Dictionary<string, object?> { ["@name"] = "dbo." + tableName },
            databaseOverride);
        return result is not null && result is not DBNull;
    }

    public async Task<bool> ColumnExistsAsync(string tableName, string columnName, string? databaseOverride = null)
    {
        const string sql = @"
            SELECT COUNT(*) FROM sys.columns c
            JOIN sys.tables t ON c.object_id = t.object_id
            WHERE t.name = @t AND c.name = @c";
        var result = await ExecuteScalarAsync(sql, new Dictionary<string, object?>
        {
            ["@t"] = tableName,
            ["@c"] = columnName
        }, databaseOverride);
        return Convert.ToInt32(result) > 0;
    }
}
