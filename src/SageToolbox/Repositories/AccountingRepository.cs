using Microsoft.Data.SqlClient;
using SageToolbox.Core;
using SageToolbox.Models;

namespace SageToolbox.Repositories;

/// <summary>
/// Lecture des exercices comptables et déclôture (#11). Portage fidèle de
/// open_accounting_window/run_accounting_script (AIG_Toolbox.py V1), étendu à 10
/// exercices (colonnes D_DebutExo01..10/D_FinExo01..10 confirmées PDF p.17-22, V1 ne
/// gérait que 5 — cf. SYNTHESE_V2.md qui décrit l'extension V12+/V2 à 10 exercices).
///
/// Le statut Ouvert/Clôturé par exercice et la règle de blocage "impossible de rouvrir
/// si un exercice plus récent est clôturé" n'existent PAS dans le source V1 (qui affiche
/// tous les exercices valides sans distinction de statut) — c'est un ajout V2 dont je
/// n'ai pas le source. On dérive le statut de D_ArchivageNN (1 = clôturé) — vérifié sur
/// une base réelle (TEST_CLAUDE : D_Archivage01-07 = 1 pour les exercices 2018-2024,
/// confirmés clôturés, D_Archivage08-09 = 0 pour 2025-2026, ouverts). Une première
/// hypothèse basée sur D_EcNoClotureNN s'est révélée fausse : ce champ reste à 0 même
/// sur des exercices réellement clôturés dans les données réelles.
/// </summary>
public sealed class AccountingRepository
{
    public async Task<int> GetMaxExercicesAsync() =>
        await DatabaseManager.Instance.ColumnExistsAsync("P_DOSSIER", "D_DebutExo06") ? 10 : 5;

    public async Task<List<ExerciceInfo>> GetExercicesAsync()
    {
        var maxExercices = await GetMaxExercicesAsync();
        var columns = string.Join(", ", Enumerable.Range(1, maxExercices).SelectMany(n => new[]
        {
            $"D_DebutExo{n:D2}", $"D_FinExo{n:D2}", $"D_Archivage{n:D2}"
        }));

        var table = await DatabaseManager.Instance.ExecuteQueryAsync($"SELECT TOP 1 {columns} FROM dbo.P_DOSSIER");
        if (table.Rows.Count == 0) return new List<ExerciceInfo>();

        var row = table.Rows[0];
        var result = new List<ExerciceInfo>();
        for (var n = 1; n <= maxExercices; n++)
        {
            var debut = row[$"D_DebutExo{n:D2}"];
            var fin = row[$"D_FinExo{n:D2}"];
            // V1 : une date de début à 1753-01-01 (valeur "vide" par défaut SQL Server) signifie exercice non défini.
            if (debut is DBNull || fin is DBNull || Convert.ToDateTime(debut).Year <= 1753) continue;

            var archivageVal = row[$"D_Archivage{n:D2}"];
            var estCloture = archivageVal is not DBNull && Convert.ToInt64(archivageVal) != 0;

            result.Add(new ExerciceInfo
            {
                Numero = n,
                DateDebut = Convert.ToDateTime(debut),
                DateFin = Convert.ToDateTime(fin),
                EstCloture = estCloture
            });
        }
        return result;
    }

    /// <summary>Portage de la lecture SERVERPROPERTY('InstanceDefaultBackupPath') (V1).</summary>
    public async Task<string> GetDefaultBackupPathAsync()
    {
        var result = await DatabaseManager.Instance.ExecuteScalarAsync(
            "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(255))");
        return result as string ?? string.Empty;
    }

    public async Task BackupDatabaseAsync(string database, string backupFilePath)
    {
        await using var conn = await DatabaseManager.Instance.OpenAutocommitConnectionAsync(databaseOverride: database);
        await using var cmd = new SqlCommand(
            $"BACKUP DATABASE {DatabaseManager.QuoteIdentifier(database)} TO DISK = @path WITH INIT", conn)
        {
            CommandTimeout = 0
        };
        cmd.Parameters.AddWithValue("@path", backupFilePath);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeclotureAsync(int numeroExercice, bool sauvegarderAvant, string? backupFilePath)
    {
        var exercices = await GetExercicesAsync();
        var cible = exercices.FirstOrDefault(e => e.Numero == numeroExercice)
            ?? throw new InvalidOperationException("Exercice introuvable.");

        if (!cible.EstCloture)
            throw new InvalidOperationException("Cet exercice n'est pas clôturé.");

        if (exercices.Any(e => e.Numero > numeroExercice && e.EstCloture))
            throw new InvalidOperationException(
                "Impossible de rouvrir cet exercice : un exercice plus récent est encore clôturé (dépendance des à-nouveaux).");

        if (sauvegarderAvant)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
                throw new InvalidOperationException("Chemin de sauvegarde requis.");

            var database = DatabaseManager.Instance.CurrentDatabase
                ?? throw new InvalidOperationException("Aucune base connectée.");
            try
            {
                await BackupDatabaseAsync(database, backupFilePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Échec de la sauvegarde préalable, déclôture annulée : {ex.Message}", ex);
            }
        }

        var n2 = numeroExercice.ToString("D2");

        await DatabaseManager.Instance.RunInTransactionAsync(async (conn, tx) =>
        {
            // Date de "dernière écriture créée" sur la période, réutilisée comme date de
            // modification factice (évite de déclencher une validation applicative sur
            // cbmodification) — portage fidèle de run_accounting_script (V1).
            await using var dateCmd = new SqlCommand(
                "SELECT MAX(cbcreation) FROM dbo.F_ECRITUREC WHERE JM_DATE >= @debut AND JM_DATE <= @fin", conn, tx);
            dateCmd.Parameters.AddWithValue("@debut", cible.DateDebut);
            dateCmd.Parameters.AddWithValue("@fin", cible.DateFin);
            var dateCreationResult = await dateCmd.ExecuteScalarAsync();
            var dateCreation = dateCreationResult is DBNull or null ? new DateTime(1753, 1, 1) : (DateTime)dateCreationResult;

            await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                @"UPDATE dbo.F_ECRITUREC SET EC_Cloture = 0, EC_Impression = 0, EC_DateCloture = '1753-01-01', cbmodification = @dateCreation
                  WHERE JM_DATE >= @debut AND JM_DATE <= @fin",
                new Dictionary<string, object?> { ["@dateCreation"] = dateCreation, ["@debut"] = cible.DateDebut, ["@fin"] = cible.DateFin });

            await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                "UPDATE dbo.F_JMOUV SET JM_Cloture = 0, JM_Impression = 0 WHERE JM_Date >= @debut AND JM_Date <= @fin",
                new Dictionary<string, object?> { ["@debut"] = cible.DateDebut, ["@fin"] = cible.DateFin });

            await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                $"UPDATE dbo.P_DOSSIER SET D_Archivage{n2} = 0");

            await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                "UPDATE dbo.P_DOSSIER SET D_CloturePeriod = DATEADD(year, -1, @fin), D_ArchivePeriod = DATEADD(year, -1, @fin)",
                new Dictionary<string, object?> { ["@fin"] = cible.DateFin });

            // Purge des lignes d'audit de la période — filtre exact porté de V1 (recherche
            // du jour de début et du jour de fin dans ADT_Description). Ce filtre est
            // volontairement imprécis dans l'original (simple correspondance de sous-chaîne
            // sur le jour du mois) ; préservé tel quel pour fidélité de comportement.
            await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                @"DELETE FROM dbo.F_AUDITCPTA
                  WHERE ADT_Description LIKE '%' + RIGHT('0' + CAST(DAY(@debut) AS VARCHAR), 2) + '%'
                    AND ADT_Description LIKE '%' + RIGHT('0' + CAST(DAY(@fin) AS VARCHAR), 2) + '%'",
                new Dictionary<string, object?> { ["@debut"] = cible.DateDebut, ["@fin"] = cible.DateFin });
        });
    }
}
