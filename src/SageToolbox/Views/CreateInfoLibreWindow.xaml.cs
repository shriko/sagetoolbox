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
        LengthInput.Text = LengthInput.IsEnabled ? StructureRepository.MaxTextLength.ToString() : string.Empty;
    }

    // Bloque à la saisie tout ce qui ne donnerait pas une longueur entre 1 et 69.
    private void LengthInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var box = (TextBox)sender;
        var candidate = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, e.Text);
        e.Handled = !IsValidLength(candidate);
    }

    private void LengthInput_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        var box = (TextBox)sender;
        var pasted = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        var candidate = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, pasted);
        if (!IsValidLength(candidate))
            e.CancelCommand();
    }

    private static bool IsValidLength(string text) =>
        text.All(char.IsAsciiDigit) && (text.Length == 0 || int.Parse(text) is >= 1 and <= StructureRepository.MaxTextLength);

    private async void Creer_Click(object sender, RoutedEventArgs e)
    {
        var table = (TableCombo.SelectedItem as StructureRepository.InfoLibreTable)?.Table;
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

        var tableText = $"{StructureRepository.LabelOf(table)} ({table})";
        var confirmMessage = MirrorTables.TryGetValue(table, out var mirror)
            ? $"Créer l'information libre \"{name}\" ({type.Label}) sur {tableText} (et automatiquement sur {StructureRepository.LabelOf(mirror)} ({mirror})) ?"
            : $"Créer l'information libre \"{name}\" ({type.Label}) sur {tableText} ?";
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
