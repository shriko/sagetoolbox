using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace SageToolbox.Core;

/// <summary>
/// Mise à jour automatique : interroge la dernière release GitHub, télécharge SageToolbox.exe
/// puis le remplace après la fermeture de l'application et la relance.
/// </summary>
public static class UpdateManager
{
    /// <summary>Dépôt GitHub (propriétaire/nom) hébergeant les releases.</summary>
    private const string Repository = "shriko/sagetoolbox";
    private const string AssetName = "SageToolbox.exe";

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// Vérifie la présence d'une nouvelle version et propose de l'installer.
    /// Retourne true si l'application doit se fermer (mise à jour lancée).
    /// Toute erreur (hors ligne, API indisponible…) est ignorée silencieusement.
    /// </summary>
    public static async Task<bool> CheckAndPromptAsync()
    {
        try
        {
            using var http = CreateClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

            var json = await http.GetStringAsync($"https://api.github.com/repos/{Repository}/releases/latest", cts.Token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) || latest <= CurrentVersion)
                return false;

            string? url = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                {
                    url = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
            if (url is null) return false;

            var answer = MessageBox.Show(
                $"Une nouvelle version de Sage Toolbox est disponible : {latest.ToString(3)} (version actuelle : {CurrentVersion.ToString(3)}).\n\nLa télécharger et l'installer maintenant ?",
                "Mise à jour disponible", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer != MessageBoxResult.Yes) return false;

            var currentExe = Environment.ProcessPath!;
            var newExe = currentExe + ".new";

            using var downloadCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await using (var source = await http.GetStreamAsync(url, downloadCts.Token))
            await using (var target = File.Create(newExe))
                await source.CopyToAsync(target, downloadCts.Token);

            LaunchReplacement(currentExe, newExe);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"Mise à jour : vérification impossible ({ex.Message})");
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SageToolbox-Updater");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>Script batch : attend la fermeture de l'exe, le remplace, puis relance.</summary>
    private static void LaunchReplacement(string currentExe, string newExe)
    {
        var script = Path.Combine(Path.GetTempPath(), $"SageToolbox_update_{Guid.NewGuid():N}.cmd");
        File.WriteAllText(script, string.Join("\r\n",
            "@echo off",
            ":retry",
            "ping -n 2 127.0.0.1 >nul",
            $"move /y \"{newExe}\" \"{currentExe}\" >nul 2>&1",
            "if errorlevel 1 goto retry",
            $"start \"\" \"{currentExe}\"",
            "del \"%~f0\""));

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }
}
