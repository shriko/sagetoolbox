using Microsoft.Data.SqlClient;
using SageToolbox.Core;

namespace SageToolbox.Enrichissement;

/// <summary>
/// Accès à F_COMPTET pour l'enrichissement des tiers (SIRET, nom, adresse, n° de TVA, mise en sommeil) :
/// lecture des tiers à rapprocher et mise à jour ligne par ligne protégée par des savepoints.
/// </summary>
public sealed class EnrichissementRepository
{
    private const int CommitBatchSize = 25;

    /// <summary>
    /// Tiers hors « en sommeil » filtrés par type et par présence d'un SIRET valide (14 caractères) —
    /// selon la case « Valider SIRET existants ».
    /// </summary>
    public async Task<List<TierRecord>> FetchTiersAsync(TierType type, bool validateExistingSiret, CancellationToken ct)
    {
        var sql = $"""
            SELECT CT_Num, CT_Intitule, CT_Adresse, CT_Complement, CT_CodePostal, CT_Ville,
                   CT_Siret, CT_Type, CT_Identifiant, CT_Sommeil
            FROM F_COMPTET
            WHERE (CT_Sommeil IS NULL OR CT_Sommeil = 0)
              {(type != TierType.Tous ? "AND CT_Type = @type" : "")}
              {(validateExistingSiret
                  ? "AND CT_Siret IS NOT NULL AND LEN(CT_Siret) = 14"
                  : "AND (CT_Siret IS NULL OR LEN(CT_Siret) <> 14)")}
            """;

        await using var conn = DatabaseManager.Instance.CreateConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = new SqlCommand(sql, conn);
        if (type != TierType.Tous) cmd.Parameters.AddWithValue("@type", (short)type);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<TierRecord>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new TierRecord
            {
                CT_Num = reader.GetString(reader.GetOrdinal("CT_Num")),
                CT_Intitule = NullableString(reader, "CT_Intitule"),
                CT_Adresse = NullableString(reader, "CT_Adresse"),
                CT_Complement = NullableString(reader, "CT_Complement"),
                CT_CodePostal = NullableString(reader, "CT_CodePostal"),
                CT_Ville = NullableString(reader, "CT_Ville"),
                CT_Siret = NullableString(reader, "CT_Siret"),
                CT_Type = (TierType)reader.GetInt16(reader.GetOrdinal("CT_Type")),
                CT_Identifiant = NullableString(reader, "CT_Identifiant"),
                CT_Sommeil = !reader.IsDBNull(reader.GetOrdinal("CT_Sommeil")) && reader.GetInt16(reader.GetOrdinal("CT_Sommeil")) != 0,
            });
        }

        return results;
    }

    /// <summary>
    /// Applique les mises à jour Sage pour l'ensemble des lignes confirmées : chaque ligne est protégée par un
    /// savepoint (annulation possible sans perdre les lignes déjà appliquées), commit groupé toutes les 25 lignes.
    /// </summary>
    public async Task<List<UpdateResult>> ApplyUpdatesAsync(
        IReadOnlyList<(string CtNum, Dictionary<string, object?> Columns, Dictionary<string, (string? Before, string? After)> AuditChanges)> updates,
        CancellationToken ct)
    {
        var results = new List<UpdateResult>();

        await using var conn = DatabaseManager.Instance.CreateConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var tx = conn.BeginTransaction();
        var sinceLastCommit = 0;

        try
        {
            for (var i = 0; i < updates.Count; i++)
            {
                var (ctNum, columns, auditChanges) = updates[i];
                var savepoint = $"sp_{i}";
                tx.Save(savepoint);

                try
                {
                    await ExecuteUpdateAsync(conn, tx, ctNum, columns, ct).ConfigureAwait(false);
                    results.Add(new UpdateResult(ctNum, true, null, auditChanges));
                }
                catch (Exception ex)
                {
                    tx.Rollback(savepoint);
                    results.Add(new UpdateResult(ctNum, false, ex.Message, auditChanges));
                }

                if (++sinceLastCommit >= CommitBatchSize)
                {
                    tx.Commit();
                    tx = conn.BeginTransaction();
                    sinceLastCommit = 0;
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return results;
    }

    private static async Task ExecuteUpdateAsync(SqlConnection conn, SqlTransaction tx, string ctNum, Dictionary<string, object?> columns, CancellationToken ct)
    {
        if (columns.Count == 0) return;

        var setClauses = columns.Keys.Select((col, idx) => $"{SqlIdentifier.Bracket(col)} = @p{idx}");
        var sql = $"UPDATE F_COMPTET SET {string.Join(", ", setClauses)} WHERE CT_Num = @ctNum";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@ctNum", ctNum);

        var i = 0;
        foreach (var value in columns.Values)
            cmd.Parameters.AddWithValue($"@p{i++}", value ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static string? NullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
