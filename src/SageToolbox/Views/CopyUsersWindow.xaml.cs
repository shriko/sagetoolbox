using System.Windows;
using System.Windows.Input;
using SageToolbox.Core;
using SageToolbox.Repositories;

namespace SageToolbox.Views;

public partial class CopyUsersWindow : Window
{
    private readonly ServerRepository _serverRepository = new();
    private readonly UsersRepository _usersRepository = new();

    public CopyUsersWindow()
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);
        Loaded += CopyUsersWindow_Loaded;
    }

    private async void CopyUsersWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var databases = await _serverRepository.DiscoverSageDatabasesAsync(DatabaseManager.Instance.CurrentInstance!);
            SourceCombo.ItemsSource = databases;
            DestCombo.ItemsSource = databases;
            var current = DatabaseManager.Instance.CurrentDatabase;
            if (current is not null && databases.Contains(current))
                SourceCombo.SelectedItem = current;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Impossible de lister les bases de l'instance : {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async void Copier_Click(object sender, RoutedEventArgs e)
    {
        var source = SourceCombo.SelectedItem as string;
        var dest = DestCombo.SelectedItem as string;

        if (source is null || dest is null)
        {
            MessageBox.Show("Merci de sélectionner une base source et une base destination.", "Copier les utilisateurs",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (source == dest)
        {
            MessageBox.Show("La base source et la base destination doivent être différentes.", "Copier les utilisateurs",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(
            $"Copier les utilisateurs de \"{source}\" vers \"{dest}\" ?\nLes utilisateurs existants dans \"{dest}\" (hors administrateur) seront écrasés.",
            "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        Mouse.OverrideCursor = Cursors.Wait;
        IsEnabled = false;
        try
        {
            var existingTables = await _usersRepository.GetExistingProtectionTablesAsync(dest);
            if (!existingTables.Contains("F_PROTECTIONCPTA"))
            {
                MessageBox.Show($"La table F_PROTECTIONCPTA n'est pas présente dans la base \"{dest}\".",
                    "Structure invalide", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var linkedCount = await _usersRepository.CountLinkedCollaborateursAsync(dest);
            if (linkedCount > 0)
            {
                Mouse.OverrideCursor = null;
                var unlink = MessageBox.Show(
                    $"{linkedCount} collaborateur(s) de \"{dest}\" sont liés à un profil de protection.\nVoulez-vous les délier avant la copie ?",
                    "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                Mouse.OverrideCursor = Cursors.Wait;
                if (unlink == MessageBoxResult.Yes)
                    await _usersRepository.UnlinkCollaborateursAsync(dest);
            }

            Logger.Instance.Info($"Copie des utilisateurs de {source} vers {dest}...");
            var rows = await _usersRepository.CopyUsersAsync(source, dest, existingTables);
            Logger.Instance.Success($"Copie terminée : {rows} ligne(s) copiée(s) vers {dest}.");
            Close();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Copie des utilisateurs : échec — {ex.Message}");
            MessageBox.Show(ex.Message, "Copie impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
        }
    }
}
