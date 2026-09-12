using System.IO;
using System.Text;
using Microsoft.Data.SqlClient;
using SageToolbox.Core;

namespace SageToolbox.Repositories;

/// <summary>
/// Génération des fichiers de connexion (#7/#14/#19). Portage de generate_connexion_files
/// (AIG_Toolbox.py V1), avec une correction : V1 recalcule le "créateur" via un
/// dictionnaire codé en dur (get_creator) qui associe TRES->MIMO et IMMO->MOPA — vérifié
/// FAUX par inspection directe d'une base Sage réelle (cbsystable.CB_Creator donne
/// IMMO->MIMO et TRES->MOPA, l'inverse). cbsystable porte déjà CB_Creator en clair : on
/// le lit donc directement plutôt que de le recalculer, ce qui élimine ce bug latent.
/// </summary>
public sealed class ConnectionFileRepository
{
    private static readonly Dictionary<string, string> ExtensionByCbType = new()
    {
        ["CPTA"] = ".mae",
        ["CIAL"] = ".gcm",
        ["TRES"] = ".mdp",
        ["IMMO"] = ".imo",
    };

    /// <summary>
    /// Encoding.UTF8 (.NET) écrit un BOM (EF BB BF) en tête de fichier par défaut ; le
    /// lecteur de fichiers .mae/.gcm/.imo/.mdp de Sage ne l'attend pas et corrompt la
    /// première clé lue (ex. "[CBASE]" devient illisible), provoquant des erreurs
    /// trompeuses à l'ouverture du dossier (confirmé : "Mauvaise version de dossier" sur
    /// un fichier généré par cet outil, absent sur un fichier UTF-8 sans BOM). D'où cet
    /// UTF8Encoding explicite sans BOM plutôt que Encoding.UTF8.
    /// </summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<List<string>> GenerateAsync(string instance, string database, string outputDirectory)
    {
        var generated = new List<string>();
        Directory.CreateDirectory(outputDirectory);

        await using var conn = DatabaseManager.Instance.CreateConnection(instance, database);
        await conn.OpenAsync();

        if (await ObjectExistsAsync(conn, "T_INSTANCES"))
        {
            var prhPath = Path.Combine(outputDirectory, database + ".prh");
            var content =
                "[CONNECTION SQL]\r\n" +
                $"Provider=MSOLEDBSQL\r\n" +
                $"Server={instance}\r\n" +
                "Trusted_Connexion=yes\r\n" +
                $"Database={database}\r\n" +
                "Application Name=Sage Application\r\n";
            await File.WriteAllTextAsync(prhPath, content, Utf8NoBom);
            generated.Add(prhPath);
        }

        if (await ObjectExistsAsync(conn, "cbsystable"))
        {
            await using var cmd = new SqlCommand("SELECT CB_Type, CB_Creator FROM dbo.cbsystable", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;
                var cbType = reader.GetString(0).Trim();
                var creator = reader.GetString(1).Trim();
                if (!ExtensionByCbType.TryGetValue(cbType, out var extension)) continue;

                var path = Path.Combine(outputDirectory, database + extension);
                var content =
                    "[CBASE]\r\n" +
                    $"ServeurSQL={instance}\r\n" +
                    $"Createur={creator}\r\n" +
                    $"Type={cbType}\r\n";
                await File.WriteAllTextAsync(path, content, Utf8NoBom);
                generated.Add(path);
            }
        }

        return generated;
    }

    private static async Task<bool> ObjectExistsAsync(SqlConnection conn, string tableName)
    {
        await using var cmd = new SqlCommand("SELECT OBJECT_ID(@name)", conn);
        cmd.Parameters.AddWithValue("@name", "dbo." + tableName);
        var result = await cmd.ExecuteScalarAsync();
        return result is not null && result is not DBNull;
    }
}
