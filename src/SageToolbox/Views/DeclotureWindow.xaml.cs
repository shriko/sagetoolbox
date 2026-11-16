using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Models;
using SageToolbox.Repositories;

namespace SageToolbox.Views;

public partial class DeclotureWindow : ToolPanel
{
    private readonly AccountingRepository _repository = new();
    private readonly List<RadioButton> _radios = new();

    public DeclotureWindow()
    {
        InitializeComponent();
        Loaded += DeclotureWindow_Loaded;

        // Pas de sauvegarde SQL possible sur le serveur Azure partagé d'un dossier SPC.
        if (DatabaseManager.Instance.IsSpc)
        {
            BackupCheckBox.IsChecked = false;
            BackupCheckBox.IsEnabled = false;
            BackupCheckBox.Content = "Sauvegarder la base avant l'opération (indisponible en SPC)";
        }
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
                    Content = BuildExerciceContent(exercice),
                    Tag = exercice,
                    IsEnabled = exercice.EstCloture,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(0, 0, 0, 2)
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

    /// <summary>Ligne d'exercice : cadenas fermé (clôturé) ou ouvert, numéro, puis période et statut.</summary>
    private static UIElement BuildExerciceContent(ExerciceInfo exercice)
    {
        var icon = new TextBlock
        {
            Text = exercice.EstCloture ? "" : "",
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 20,
            Width = 28,
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetResourceReference(TextBlock.ForegroundProperty,
            exercice.EstCloture ? "AccentBrush" : "TextSecondaryBrush");

        var title = new TextBlock { Text = $"Exercice {exercice.Numero}", FontWeight = FontWeights.SemiBold };
        var detail = new TextBlock
        {
            Text = $"{exercice.DateDebut:dd/MM/yyyy} - {exercice.DateFin:dd/MM/yyyy}  ·  {(exercice.EstCloture ? "Clôturé" : "Ouvert")}",
            FontSize = 12
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(title);
        text.Children.Add(detail);

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(icon);
        panel.Children.Add(text);
        return panel;
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
