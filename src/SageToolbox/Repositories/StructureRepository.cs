using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using SageToolbox.Core;

namespace SageToolbox.Repositories;

/// <summary>
/// Création d'une Information Libre (#13). Portage fidèle de create_info_libre/
/// open_creation_il (AIG_Toolbox.py V1) : liste des tables cibles, types (avec leurs
/// codes cbSysLibre.CB_Type/CB_Len d'origine) et structure du catalogue cbSysLibre
/// (CB_File, CB_Name, CB_Pos, CB_Type, CB_Len, CB_Flag) copiés à l'identique.
///
/// Miroir automatique F_DOCENTETE -> F_ABOENTETE et F_DOCLIGNE -> F_ABOLIGNE (demande
/// explicite) : les deux créations (table cible + miroir) se font dans UNE même
/// transaction, pour ne jamais laisser les deux tables désynchronisées si l'une échoue.
/// </summary>
public sealed class StructureRepository
{
    /// <summary>Table Sage acceptant des informations libres, avec son libellé affiché à l'utilisateur.</summary>
    public sealed record InfoLibreTable(string Table, string Label);

    public static readonly InfoLibreTable[] TargetTables =
    {
        new("F_COMPTEG", "Comptes généraux"),
        new("F_COMPTEA", "Comptes analytiques"),
        new("F_COMPTET", "Tiers"),
        new("F_ECRITUREC", "Écritures comptables"),
        new("F_IMMOBILISATION", "Immobilisations"),
        new("F_RESSOURCEPROD", "Ressources"),
        new("F_ARTICLE", "Articles"),
        new("F_LOTSERIE", "Numéros de série / lot"),
        new("F_DOCENTETE", "Entêtes de documents"),
        new("F_DOCLIGNE", "Lignes de documents"),
        new("F_ABOENTETE", "Entêtes d'abonnements"),
        new("F_ABOLIGNE", "Lignes d'abonnements"),
    };

    public static string LabelOf(string table) =>
        TargetTables.FirstOrDefault(t => t.Table == table)?.Label ?? table;

    private static readonly Dictionary<string, string> MirrorTables = new()
    {
        ["F_DOCENTETE"] = "F_ABOENTETE",
        ["F_DOCLIGNE"] = "F_ABOLIGNE",
    };

    /// <summary>
    /// Longueur maximale (et par défaut) d'une information libre texte : Sage crée ces
    /// colonnes en VARCHAR(69) et enregistre CB_Len = 70 dans cbSysLibre (longueur + 1).
    /// </summary>
    public const int MaxTextLength = 69;

    public sealed record InfoLibreType(string Label, int CbType, int CbLen, string SqlType, bool AllowsCustomLength);

    public static readonly InfoLibreType[] Types =
    {
        new("Texte", 9, MaxTextLength + 1, $"VARCHAR({MaxTextLength})", true),
        new("Date", 3, 2, "smalldatetime", false),
        new("Date Longue", 14, 4, "datetime", false),
        new("Montant", 20, 8, "numeric(24,6)", false),
        new("Table", 22, 36, "VARCHAR(35)", false),
        new("Valeur", 7, 8, "numeric(24,6)", false),
    };

    private static readonly Regex NameRegex = new(@"^[A-Za-z_][A-Za-z0-9_]{0,127}$");

    public async Task CreateInfoLibreAsync(string table, string name, InfoLibreType type, int? textLength)
    {
        if (!TargetTables.Any(t => t.Table == table))
            throw new InvalidOperationException("Table cible invalide.");
        if (!NameRegex.IsMatch(name))
            throw new InvalidOperationException(
                "Le nom doit être un identifiant SQL valide (lettres, chiffres, underscore, ne commence pas par un chiffre).");

        var cbType = type.CbType;
        var cbLen = type.CbLen;
        var sqlType = type.SqlType;

        if (type.AllowsCustomLength && textLength is { } len)
        {
            if (len is < 1 or > MaxTextLength)
                throw new InvalidOperationException($"La longueur doit être comprise entre 1 et {MaxTextLength}.");
            cbLen = len + 1;
            sqlType = $"VARCHAR({len})";
        }

        await DatabaseManager.Instance.RunInTransactionAsync(async (conn, tx) =>
        {
            await CreateOnTableAsync(conn, tx, table, name, sqlType, cbType, cbLen);

            if (MirrorTables.TryGetValue(table, out var mirrorTable))
                await CreateOnTableAsync(conn, tx, mirrorTable, name, sqlType, cbType, cbLen);
        });
    }

    private static async Task CreateOnTableAsync(
        SqlConnection conn, SqlTransaction tx, string table, string name, string sqlType, int cbType, int cbLen)
    {
        var tableQuoted = DatabaseManager.QuoteIdentifier(table);
        var nameQuoted = DatabaseManager.QuoteIdentifier(name);

        await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
            $"ALTER TABLE dbo.{tableQuoted} ADD {nameQuoted} {sqlType} NULL");

        await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
            @"INSERT INTO dbo.cbSysLibre (CB_File, CB_Name, CB_Pos, CB_Type, CB_Len, CB_Flag)
              VALUES (@file, @name, (SELECT ISNULL(MAX(CB_Pos), 0) + 1 FROM dbo.cbSysLibre WHERE CB_File = @file), @type, @len, 0)",
            new Dictionary<string, object?>
            {
                ["@file"] = table,
                ["@name"] = name,
                ["@type"] = cbType,
                ["@len"] = cbLen
            });
    }
}
