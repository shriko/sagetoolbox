using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Repositories;

namespace SageToolbox.Views;

public partial class RestorationWindow : Window
{
    private readonly RestorationRepository _repository = new();

    public RestorationWindow()
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);

        BakFolderInput.Text = ConfigManager.Instance.Config.LastRestoreBakFolder ?? string.Empty;
        ConnFilesFolderInput.Text = ConfigManager.Instance.Config.LastRestoreConnFilesFolder ?? string.Empty;
        ReplaceExistingCheckBox.IsChecked = ConfigManager.Instance.Config.LastRestoreReplaceExisting;
    }

    private void BrowseBakButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Dossier des fichiers .bak" };
        if (dialog.ShowDialog() == true)
            BakFolderInput.Text = dialog.FolderName;
    }

    private void BrowseConnFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Dossier des fichiers de connexion" };
        if (dialog.ShowDialog() == true)
            ConnFilesFolderInput.Text = dialog.FolderName;
    }

    private async void Restaurer_Click(object sender, RoutedEventArgs e)
    {
        var bakFolder = BakFolderInput.Text.Trim();
        var connFilesFolder = ConnFilesFolderInput.Text.Trim();
        var replaceExisting = ReplaceExistingCheckBox.IsChecked == true;

        if (string.IsNullOrEmpty(bakFolder) || string.IsNullOrEmpty(connFilesFolder))
        {
            MessageBox.Show("Merci de renseigner le dossier des .bak et le dossier des fichiers de connexion.",
                "Restauration SQL", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(bakFolder))
        {
            MessageBox.Show($"Le dossier \"{bakFolder}\" est introuvable.", "Restauration SQL",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var bakFiles = Directory.GetFiles(bakFolder, "*.bak", SearchOption.TopDirectoryOnly);
        if (bakFiles.Length == 0)
        {
            MessageBox.Show("Aucun fichier .bak trouvé dans ce dossier.", "Restauration SQL",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var warning = replaceExisting
            ? $"{bakFiles.Length} fichier(s) .bak trouvé(s).\n\nLes bases existantes du même nom seront ÉCRASÉES. Aucun utilisateur ne doit être connecté aux bases concernées."
            : $"{bakFiles.Length} fichier(s) .bak trouvé(s). Les bases déjà existantes seront ignorées.";

        if (MessageBox.Show($"Lancer la restauration ?\n\n{warning}", "Confirmation",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        ConfigManager.Instance.Config.LastRestoreBakFolder = bakFolder;
        ConfigManager.Instance.Config.LastRestoreConnFilesFolder = connFilesFolder;
        ConfigManager.Instance.Config.LastRestoreReplaceExisting = replaceExisting;
        ConfigManager.Instance.Save();

        var instance = DatabaseManager.Instance.CurrentInstance!;

        Mouse.OverrideCursor = Cursors.Wait;
        IsEnabled = false;
        try
        {
            Logger.Instance.Info($"Restauration SQL en lot : {bakFiles.Length} fichier(s) depuis \"{bakFolder}\"...");
            var results = await _repository.RestoreAllAsync(instance, bakFolder, connFilesFolder, replaceExisting);

            var restored = results.Count(r => r.Outcome is RestorationRepository.RestoreOutcome.Restored or RestorationRepository.RestoreOutcome.Replaced);
            var skipped = results.Count(r => r.Outcome == RestorationRepository.RestoreOutcome.AlreadyExists);
            var failed = results.Count(r => r.Outcome == RestorationRepository.RestoreOutcome.Failed);

            Logger.Instance.Success($"Restauration terminée : {restored} restaurée(s), {skipped} ignorée(s), {failed} échec(s).");
            Close();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Restauration SQL : échec — {ex.Message}");
            MessageBox.Show(ex.Message, "Restauration impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
        }
    }
}
