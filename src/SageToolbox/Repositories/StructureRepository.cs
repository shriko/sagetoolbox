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
    public static readonly string[] TargetTables =
    {
        "F_COMPTEG", "F_COMPTEA", "F_COMPTET", "F_ECRITUREC", "F_IMMOBILISATION",
        "F_RESSOURCEPROD", "F_DOCENTETE", "F_DOCLIGNE", "F_ABOENTETE", "F_ABOLIGNE"
    };

    private static readonly Dictionary<string, string> MirrorTables = new()
    {
        ["F_DOCENTETE"] = "F_ABOENTETE",
        ["F_DOCLIGNE"] = "F_ABOLIGNE",
    };

    public sealed record InfoLibreType(string Label, int CbType, int CbLen, string SqlType, bool AllowsCustomLength);

    public static readonly InfoLibreType[] Types =
    {
        new("Texte", 9, 70, "VARCHAR", true),
        new("Date", 3, 2, "smalldatetime", false),
        new("Date Longue", 14, 4, "datetime", false),
        new("Montant", 20, 8, "numeric(24,6)", false),
        new("Table", 22, 36, "VARCHAR(35)", false),
        new("Valeur", 7, 8, "numeric(24,6)", false),
    };

    private static readonly Regex NameRegex = new(@"^[A-Za-z_][A-Za-z0-9_]{0,127}$");

    public async Task CreateInfoLibreAsync(string table, string name, InfoLibreType type, int? textLength)
    {
        if (!TargetTables.Contains(table))
            throw new InvalidOperationException("Table cible invalide.");
        if (!NameRegex.IsMatch(name))
            throw new InvalidOperationException(
                "Le nom doit être un identifiant SQL valide (lettres, chiffres, underscore, ne commence pas par un chiffre).");

        var cbType = type.CbType;
        var cbLen = type.CbLen;
        var sqlType = type.SqlType;

        if (type.AllowsCustomLength && textLength is { } len)
        {
            if (len is < 1 or > 255)
                throw new InvalidOperationException("La longueur doit être comprise entre 1 et 255.");
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
