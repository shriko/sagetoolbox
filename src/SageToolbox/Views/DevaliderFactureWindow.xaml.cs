using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using SageToolbox.Core;
using SageToolbox.Models;
using SageToolbox.Repositories;

namespace SageToolbox.Views;

public partial class DevaliderFactureWindow : ToolPanel
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private readonly DocumentRepository _repository = new();
    private readonly ObservableCollection<FactureValidee> _factures = new();

    public DevaliderFactureWindow()
    {
        InitializeComponent();
        FacturesList.ItemsSource = _factures;
        Loaded += async (_, _) => await SearchAsync();
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync();

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        DateFromInput.Clear();
        DateToInput.Clear();
        ClientInput.Clear();
        PieceInput.Clear();
        _ = SearchAsync();
    }

    private async void Filters_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await SearchAsync();
    }

    private bool TryParseDate(string text, string label, out DateTime? date)
    {
        date = null;
        text = text.Trim();
        if (text.Length == 0) return true;
        if (DateTime.TryParse(text, Fr, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }
        MessageBox.Show($"Date « {label} » invalide : « {text} » (format attendu : jj/mm/aaaa).",
            "Dévalider des factures", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private async Task SearchAsync()
    {
        if (!TryParseDate(DateFromInput.Text, "du", out var from)) return;
        if (!TryParseDate(DateToInput.Text, "au", out var to)) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var (factures, truncated) = await _repository.GetFacturesValideesAsync(
                from, to, ClientInput.Text.Trim(), PieceInput.Text.Trim());

            foreach (var old in _factures) old.PropertyChanged -= OnFactureChanged;
            _factures.Clear();
            foreach (var facture in factures)
            {
                facture.PropertyChanged += OnFactureChanged;
                _factures.Add(facture);
            }

            var blocked = factures.Count(f => f.IsBlocked);
            LimitText.Text = truncated
                ? $"Liste limitée aux {DocumentRepository.MaxResults} factures les plus récentes : affinez les filtres."
                : blocked > 0 ? $"{blocked} facture(s) transmise(s) en facturation électronique : dévalidation impossible." : string.Empty;
            SelectAllCheckBox.IsChecked = false;
            UpdateSelection();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Recherche des factures validées : {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnFactureChanged(object? sender, PropertyChangedEventArgs e) => UpdateSelection();

    private void UpdateSelection()
    {
        var selected = _factures.Count(f => f.IsChecked);
        CountText.Text = $"{_factures.Count} facture(s) affichée(s) — {selected} sélectionnée(s)";
        DevaliderButton.IsEnabled = selected > 0;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        var check = SelectAllCheckBox.IsChecked == true;
        foreach (var facture in _factures.Where(f => !f.IsBlocked))
            facture.IsChecked = check;
    }

    private void FacturesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var blocked = FacturesList.SelectedItem is FactureValidee { IsBlocked: true } facture ? facture : null;
        ReasonBanner.Visibility = blocked is null ? Visibility.Collapsed : Visibility.Visible;
        ReasonText.Text = blocked is null ? string.Empty : $"Facture {blocked.DoPiece} : {blocked.BlockReason}";
    }

    private async void Devalider_Click(object sender, RoutedEventArgs e)
    {
        var selection = _factures.Where(f => f.IsChecked && !f.IsBlocked).ToList();
        if (selection.Count == 0) return;

        var preview = string.Join("\n", selection.Take(10).Select(f => $"  {f.DoPiece} — {f.CtIntitule}"));
        if (selection.Count > 10) preview += $"\n  … et {selection.Count - 10} autre(s)";
        if (MessageBox.Show($"Dévalider {selection.Count} facture(s) ?\n\n{preview}", "Confirmation",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Mouse.OverrideCursor = Cursors.Wait;
        IsEnabled = false;
        var done = 0;
        var failed = 0;
        try
        {
            foreach (var facture in selection)
            {
                try
                {
                    Logger.Instance.Info($"Dévalidation de la facture {facture.DoPiece}...");
                    await _repository.DevaliderAsync(facture);
                    Logger.Instance.Success($"Facture {facture.DoPiece} dévalidée.");
                    done++;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"Facture {facture.DoPiece} : dévalidation impossible — {ex.Message}");
                    failed++;
                }
            }
            Logger.Instance.Info($"Dévalidation terminée : {done} dévalidée(s), {failed} échec(s).");
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
        }

        await SearchAsync();
    }
}
