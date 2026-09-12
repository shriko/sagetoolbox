using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SageToolbox.Core;
using SageToolbox.Repositories;

namespace SageToolbox.Views;

public partial class CreateInfoLibreWindow : Window
{
    private static readonly Dictionary<string, string> MirrorTables = new()
    {
        ["F_DOCENTETE"] = "F_ABOENTETE",
        ["F_DOCLIGNE"] = "F_ABOLIGNE",
    };

    private readonly StructureRepository _repository = new();

    public CreateInfoLibreWindow()
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);
        TableCombo.ItemsSource = StructureRepository.TargetTables;
        TypeCombo.ItemsSource = StructureRepository.Types;
        Loaded += (_, _) => NameInput.Focus();
    }

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var type = TypeCombo.SelectedItem as StructureRepository.InfoLibreType;
        LengthInput.IsEnabled = type?.AllowsCustomLength == true;
        LengthInput.Text = LengthInput.IsEnabled ? "70" : string.Empty;
    }

    private async void Creer_Click(object sender, RoutedEventArgs e)
    {
        var table = TableCombo.SelectedItem as string;
        var name = NameInput.Text.Trim();
        var type = TypeCombo.SelectedItem as StructureRepository.InfoLibreType;

        if (table is null || string.IsNullOrEmpty(name) || type is null)
        {
            MessageBox.Show("Tous les champs doivent être remplis.", "Créer une Information Libre",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int? textLength = null;
        if (type.AllowsCustomLength && !string.IsNullOrWhiteSpace(LengthInput.Text))
        {
            if (!int.TryParse(LengthInput.Text, out var len))
            {
                MessageBox.Show("La longueur doit être un nombre valide.", "Créer une Information Libre",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            textLength = len;
        }

        var confirmMessage = MirrorTables.TryGetValue(table, out var mirror)
            ? $"Créer l'information libre \"{name}\" ({type.Label}) sur {table} (et automatiquement sur {mirror}) ?"
            : $"Créer l'information libre \"{name}\" ({type.Label}) sur {table} ?";
        if (MessageBox.Show(confirmMessage, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Mouse.OverrideCursor = Cursors.Wait;
        IsEnabled = false;
        try
        {
            Logger.Instance.Info($"Création de l'information libre {name} sur {table}...");
            await _repository.CreateInfoLibreAsync(table, name, type, textLength);

            if (MirrorTables.TryGetValue(table, out var mirrorTable))
                Logger.Instance.Success($"Information libre {name} ({type.Label}) créée sur {table} et miroir automatique sur {mirrorTable}.");
            else
                Logger.Instance.Success($"Information libre {name} ({type.Label}) créée sur {table}.");
            Close();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Création de l'information libre : échec — {ex.Message}");
            MessageBox.Show(ex.Message, "Création impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
        }
    }
}
