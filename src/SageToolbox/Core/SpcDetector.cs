using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace SageToolbox.Core;

/// <summary>Base SQL Azure Sage Partner Cloud retrouvée dans la configuration locale de Sage 100.
/// Le mot de passe ne doit jamais être journalisé ni enregistré.</summary>
public sealed record DetectedSpcDatabase(string Server, string Database, string Login, string Password);

/// <summary>Détection d'un environnement SPC : Sage 100 pose dans le registre du poste le serveur Azure, la base
/// et le mot de passe chiffré (DPAPI, portée machine, entropie dérivée du SiteID). Rien n'est cherché sur le réseau.</summary>
public static class SpcDetector
{
    private const string CBaseKey = @"SOFTWARE\WOW6432Node\Sage\CBase";
    private const string SiteKey = @"SOFTWARE\Sage\Sage100Online";

    /// <summary>Bases Sage 100 Azure déclarées sur le poste (liste vide si le poste n'est pas relié à SPC).</summary>
    public static List<DetectedSpcDatabase> Detect()
    {
        var result = new List<DetectedSpcDatabase>();
        try
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = hklm.OpenSubKey(CBaseKey);
            var server = key?.GetValue("azureServerName") as string;
            if (key is null || string.IsNullOrWhiteSpace(server)) return result;

            string[] names;
            using (var databases = key.OpenSubKey("Databases"))
                names = databases?.GetSubKeyNames() ?? Array.Empty<string>();
            if (names.Length == 0 && key.GetValue("azureDatabaseName") is string single && !string.IsNullOrWhiteSpace(single))
                names = new[] { single };

            var password = ReadPassword(hklm, key);
            if (password is null) return result;

            foreach (var name in names)
                result.Add(new DetectedSpcDatabase(server.Trim(), name, name + "_user_cbase", password));
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"Détection SPC impossible : {ex.Message}");
            result.Clear();
        }
        return result;
    }

    private static string? ReadPassword(RegistryKey hklm, RegistryKey cbase)
    {
        if (cbase.GetValue("AzureDBPwd") is not byte[] blob) return null;

        string? siteId = null;
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var site = baseKey.OpenSubKey(SiteKey);
            siteId = site?.GetValue("SiteID") as string;
            if (!string.IsNullOrEmpty(siteId)) break;
        }

        var entropy = SHA256.HashData(Encoding.UTF8.GetBytes(siteId ?? string.Empty));
        return Encoding.Unicode.GetString(ProtectedData.Unprotect(blob, entropy, DataProtectionScope.LocalMachine));
    }
}
