using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Rfe;

namespace SageToolbox.Views;

/// <summary>Listing des adresses de facturation électronique (maillage RFE) des clients de la base connectée.</summary>
public partial class ListingRfePanel : ToolPanel
{
    private const string All = "Tous";

    private readonly RfeRepository _repository = new();
    private readonly ObservableCollection<RfeAddress> _rows = new();
    private readonly ICollectionView _view;

    public ListingRfePanel()
    {
        InitializeComponent();

        ActifFilter.ItemsSource = new[] { All, "Oui", "Non" };
        ActifFilter.SelectedIndex = 0;
        TypeFilter.ItemsSource = new[] { All };
        TypeFilter.SelectedIndex = 0;

        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = o => o is RfeAddress row && Matches(row);
        _view.SortDescriptions.Add(new SortDescription(nameof(RfeAddress.Compte), ListSortDirection.Ascending));
        _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RfeAddress.Compte)));
        Grid.ItemsSource = _view;

        Loaded += async (_, _) => await LoadAsync();
    }

    // ----- Chargement -----

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        BusyBar.Visibility = Visibility.Visible;
        StatusText.Text = "Chargement des données...";
        try
        {
            var addresses = await _repository.LoadAsync();

            _rows.Clear();
            foreach (var address in addresses) _rows.Add(address);

            var previous = TypeFilter.SelectedItem as string ?? All;
            var types = new List<string> { All };
            types.AddRange(addresses.Select(a => a.TypeMaille).Distinct().OrderBy(t => t));
            TypeFilter.ItemsSource = types;
            TypeFilter.SelectedItem = types.Contains(previous) ? previous : All;

            UpdateStatus($"{_rows.Count} adresse(s) chargée(s) — {DateTime.Now:HH:mm:ss}");
            Logger.Instance.Info($"Listing RFE : {_rows.Count} adresse(s) de facturation électronique chargée(s).");
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            var message = "Cette base ne contient pas les adresses de facturation électronique (table F_COMPTETFACTRECEPTION) : Sage 100 v12 est requis.";
            StatusText.Text = message;
            Logger.Instance.Error($"Listing RFE : {message}");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Erreur lors du chargement des données.";
            Logger.Instance.Error($"Listing RFE : chargement impossible — {ex.Message}");
            MessageBox.Show($"Erreur lors du chargement des données :\n{ex.Message}", "Listing RFE", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BusyBar.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateStatus(string prefix)
    {
        var shown = _view.Cast<object>().Count();
        StatusText.Text = shown == _rows.Count ? prefix : $"{prefix} — {shown} affichée(s) après filtre";
    }

    // ----- Filtres -----

    private bool Matches(RfeAddress row)
    {
        var compte = CompteFilter.Text.Trim();
        if (compte.Length > 0 && !row.Compte.Contains(compte, StringComparison.OrdinalIgnoreCase)) return false;

        var raison = RaisonFilter.Text.Trim();
        if (raison.Length > 0 && !row.RaisonSociale.Contains(raison, StringComparison.OrdinalIgnoreCase)) return false;

        if (ActifFilter.SelectedItem is string actif && actif != All && row.Actif != actif) return false;
        if (TypeFilter.SelectedItem is string type && type != All && row.TypeMaille != type) return false;
        return true;
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _view.Refresh();
        UpdateStatus($"{_rows.Count} adresse(s) chargée(s)");
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        CompteFilter.Clear();
        RaisonFilter.Clear();
        ActifFilter.SelectedIndex = 0;
        TypeFilter.SelectedIndex = 0;
    }

    // ----- Colonnes -----

    private void Columns_Click(object sender, RoutedEventArgs e) => ColumnsPopup.IsOpen = true;

    private void Columns_Changed(object sender, RoutedEventArgs e)
    {
        static Visibility Of(CheckBox box) => box.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ColRaison.Visibility = Of(ChkRaison);
        ColTel.Visibility = Of(ChkTel);
        ColEmail.Visibility = Of(ChkEmail);
        ColType.Visibility = Of(ChkType);
        ColMaille.Visibility = Of(ChkMaille);
        ColActif.Visibility = Of(ChkActif);
        ColNb.Visibility = Of(ChkNb);
        ColContact.Visibility = Of(ChkContact);
    }

    // ----- Export Excel -----

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var visible = _view.Cast<RfeAddress>().ToList();
        if (visible.Count == 0)
        {
            MessageBox.Show("Aucune donnée à exporter.", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Exporter la liste vers Excel",
            Filter = "Classeur Excel (*.xlsx)|*.xlsx",
            FileName = $"Maillage_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            RfeExcelExport.Export(dialog.FileName, visible, new RfeExcelColumns(
                RaisonSociale: ChkRaison.IsChecked == true,
                Telephone: ChkTel.IsChecked == true,
                Email: ChkEmail.IsChecked == true,
                TypeMaille: ChkType.IsChecked == true,
                MailleAdresse: ChkMaille.IsChecked == true,
                Actif: ChkActif.IsChecked == true,
                NbAdresses: ChkNb.IsChecked == true,
                Contact: ChkContact.IsChecked == true));
            StatusText.Text = $"Export réalisé : {dialog.FileName}";
            Logger.Instance.Success($"Listing RFE : export Excel réalisé ({visible.Count} ligne(s)) : {dialog.FileName}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Listing RFE : export Excel impossible — {ex.Message}");
            MessageBox.Show($"Erreur lors de l'export Excel :\n{ex.Message}", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ----- Liens téléphone / e-mail -----

    private void TelHyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Hyperlink { Tag: string phone } || string.IsNullOrWhiteSpace(phone)) return;

        var digits = Regex.Replace(phone, @"[^\d+]", "");
        if (digits.Length > 0) LaunchUri($"tel:{digits}");
    }

    private void MailHyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { Tag: string email } && !string.IsNullOrWhiteSpace(email)) LaunchUri($"mailto:{email}");
    }

    private static void LaunchUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ouvrir « {uri} ».\nAucune application n'est peut-être associée à ce type de lien.\n\n{ex.Message}",
                "Listing RFE", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
