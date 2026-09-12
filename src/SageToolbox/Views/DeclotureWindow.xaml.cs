using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Models;
using SageToolbox.Repositories;

namespace SageToolbox.Views;

public partial class DeclotureWindow : Window
{
    private readonly AccountingRepository _repository = new();
    private readonly List<RadioButton> _radios = new();

    public DeclotureWindow()
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);
        Loaded += DeclotureWindow_Loaded;
    }

    private async void DeclotureWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var exercices = await _repository.GetExercicesAsync();
            if (exercices.Count == 0)
            {
                ExercicesPanel.Children.Add(new TextBlock
                {
                    Text = "Aucun exercice trouvé dans P_DOSSIER.",
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (var exercice in exercices)
            {
                var radio = new RadioButton
                {
                    GroupName = "Exercices",
                    Content = exercice.Libelle,
                    Tag = exercice,
                    IsEnabled = exercice.EstCloture,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                _radios.Add(radio);
                ExercicesPanel.Children.Add(radio);
            }

            var firstClosable = _radios.FirstOrDefault(r => r.IsEnabled);
            if (firstClosable != null) firstClosable.IsChecked = true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Impossible de charger les exercices : {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async void Valider_Click(object sender, RoutedEventArgs e)
    {
        var selected = _radios.FirstOrDefault(r => r.IsChecked == true);
        if (selected is null)
        {
            MessageBox.Show("Merci de sélectionner un exercice clôturé.", "Déclôture", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var exercice = (ExerciceInfo)selected.Tag;

        if (MessageBox.Show($"Rouvrir l'exercice {exercice.Numero} ({exercice.DateDebut:dd/MM/yyyy} - {exercice.DateFin:dd/MM/yyyy}) ?",
            "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        string? backupPath = null;
        if (BackupCheckBox.IsChecked == true)
        {
            var defaultDir = await _repository.GetDefaultBackupPathAsync();
            var dialog = new SaveFileDialog
            {
                Title = "Emplacement de la sauvegarde",
                Filter = "Sauvegarde SQL Server (*.bak)|*.bak",
                FileName = $"{DatabaseManager.Instance.CurrentDatabase}_avt_traitcpta_{DateTime.Now:yyyyMMdd}.bak",
                InitialDirectory = string.IsNullOrWhiteSpace(defaultDir) ? string.Empty : defaultDir
            };
            if (dialog.ShowDialog() != true) return;
            backupPath = dialog.FileName;
        }

        Mouse.OverrideCursor = Cursors.Wait;
        IsEnabled = false;
        try
        {
            Logger.Instance.Info($"Déclôture de l'exercice {exercice.Numero}...");
            await _repository.DeclotureAsync(exercice.Numero, BackupCheckBox.IsChecked == true, backupPath);
            Logger.Instance.Success($"Exercice {exercice.Numero} rouvert.");
            Close();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Déclôture : échec — {ex.Message}");
            MessageBox.Show(ex.Message, "Déclôture impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
        }
    }
}
