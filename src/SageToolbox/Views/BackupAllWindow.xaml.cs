using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Repositories;

namespace SageToolbox.Views;

public partial class BackupAllWindow : Window
{
    private readonly BackupRepository _repository = new();

    public BackupAllWindow()
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);

        BackupFolderInput.Text = ConfigManager.Instance.Config.LastBackupFolder ?? string.Empty;
        CompressionCheckBox.IsChecked = ConfigManager.Instance.Config.LastBackupUseCompression;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Dossier de destination des sauvegardes" };
        if (dialog.ShowDialog() == true)
            BackupFolderInput.Text = dialog.FolderName;
    }

    private async void Sauvegarder_Click(object sender, RoutedEventArgs e)
    {
        var backupFolder = BackupFolderInput.Text.Trim();
        var useCompression = CompressionCheckBox.IsChecked == true;

        if (string.IsNullOrEmpty(backupFolder))
        {
            MessageBox.Show("Merci de renseigner le dossier de destination.", "Sauvegarder toutes les bases",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var instance = DatabaseManager.Instance.CurrentInstance!;

        List<string> databases;
        try
        {
            databases = await _repository.GetAllUserDatabasesAsync(instance);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de lister les bases de l'instance : {ex.Message}", "Sauvegarder toutes les bases",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (databases.Count == 0)
        {
            MessageBox.Show("Aucune base utilisateur trouvée sur cette instance.", "Sauvegarder toutes les bases",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(
            $"Sauvegarder {databases.Count} base(s) vers \"{backupFolder}\" ?\n\n{string.Join(", ", databases)}",
            "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        ConfigManager.Instance.Config.LastBackupFolder = backupFolder;
        ConfigManager.Instance.Config.LastBackupUseCompression = useCompression;
        ConfigManager.Instance.Save();

        Mouse.OverrideCursor = Cursors.Wait;
        IsEnabled = false;
        try
        {
            Logger.Instance.Info($"Sauvegarde de {databases.Count} base(s) vers \"{backupFolder}\"...");
            var results = await _repository.BackupAllAsync(instance, backupFolder, useCompression);

            var succeeded = results.Count(r => r.Success);
            var failed = results.Count(r => !r.Success);
            Logger.Instance.Success($"Sauvegarde terminée : {succeeded} réussie(s), {failed} échec(s).");
            Close();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Sauvegarde de toutes les bases : échec — {ex.Message}");
            MessageBox.Show(ex.Message, "Sauvegarde impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
        }
    }
}
