using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Enrichissement;

namespace SageToolbox.Views;

/// <summary>
/// Enrichissement des tiers : rapproche les SIRET/SIREN des fiches clients/fournisseurs de la base connectée avec l'API
/// « Recherche d'entreprises » (données INSEE/Sirene), puis répercute les corrections confirmées dans Sage.
/// </summary>
public partial class EnrichissementPanel : ToolPanel
{
    /// <summary>Ligne du tableau : enveloppe liable autour d'un <see cref="ResultRow"/> du moteur de rapprochement.</summary>
    public sealed class RowVm : INotifyPropertyChanged
    {
        private bool _confirmed;

        public RowVm(ResultRow model)
        {
            Model = model;
            _confirmed = model.Confirmed;
        }

        public ResultRow Model { get; }

        public bool Confirmed
        {
            get => _confirmed;
            set
            {
                if (_confirmed == value) return;
                _confirmed = value;
                Model.Confirmed = value;
                Raise(nameof(Confirmed));
            }
        }

        public string CtNum => Model.Tier.CT_Num;
        public string? Intitule => Model.Tier.CT_Intitule;
        public string? Adresse => Model.Tier.CT_Adresse;
        public string? Complement => Model.Tier.CT_Complement;
        public string? CodePostal => Model.Tier.CT_CodePostal;
        public string? Ville => Model.Tier.CT_Ville;
        public string? SiretSage => Model.Tier.CT_Siret;

        public string StatutText => Model switch
        {
            { OriginEntrepriseRadiee: true } => $"{Model.Status.ToDisplayString()} (origine radiée)",
            { OriginEtablissementFerme: true } => $"{Model.Status.ToDisplayString()} (établissement d'origine fermé)",
            _ => Model.Status.ToDisplayString(),
        };

        public string? SiretApi => Model.SiretApi;
        public string NomApi => Model.ManuallyCorrected ? $"{Model.NomApi} ✎" : Model.NomApi ?? string.Empty;
        public string? AdresseApi => Model.AdresseApi;
        public string? TvaApi => Model.TvaApi;

        public bool IsCheckable => Model.Status.IsCheckable();
        public bool HasCandidates => Model.Candidates.Count > 0;
        public bool IsCorrected => Model.ManuallyCorrected;

        /// <summary>Famille de couleur de la ligne : ok (vert), warn (orange), ko (rouge), ignored (gris).</summary>
        public string Group => Model.Status switch
        {
            MatchStatus.Valide or MatchStatus.Trouve => "ok",
            MatchStatus.Multiples or MatchStatus.AVerifier => "warn",
            MatchStatus.NonTrouve or MatchStatus.Erreur or MatchStatus.Radie => "ko",
            MatchStatus.Ignore => "ignored",
            _ => "",
        };

        /// <summary>Recalcule les propriétés dérivées après une mutation du modèle (résultat API, statut, etc.).</summary>
        public void Refresh()
        {
            Confirmed = Model.Confirmed;
            foreach (var name in new[]
                     {
                         nameof(StatutText), nameof(SiretApi), nameof(NomApi), nameof(AdresseApi), nameof(TvaApi),
                         nameof(IsCheckable), nameof(HasCandidates), nameof(IsCorrected), nameof(Group),
                     })
                Raise(name);
        }

        public void ToggleConfirmed()
        {
            if (IsCheckable) Confirmed = !Confirmed;
        }

        public void Ignore()
        {
            Model.Status = MatchStatus.Ignore;
            Model.Confirmed = false;
            Refresh();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed record StatusFilter(string Label, MatchStatus? Status)
    {
        public override string ToString() => Label;
    }

    private static readonly StatusFilter[] StatusFilters =
    {
        new("Tous les statuts", null),
        new("Trouvé", MatchStatus.Trouve),
        new("Radié", MatchStatus.Radie),
        new("Valide", MatchStatus.Valide),
        new("Multiples", MatchStatus.Multiples),
        new("A vérifier", MatchStatus.AVerifier),
        new("Non trouvé", MatchStatus.NonTrouve),
        new("Erreur", MatchStatus.Erreur),
        new("Ignoré", MatchStatus.Ignore),
    };

    private readonly EnrichissementRepository _repository = new();
    private readonly AuditLogger _auditLogger = new();
    private readonly ProcessingController _processing;
    private readonly ObservableCollection<RowVm> _rows = new();
    private readonly ICollectionView _view;
    private readonly ConcurrentQueue<ResultRow> _pendingResults = new();
    private readonly DispatcherTimer _flushTimer;
    private volatile bool _stopRequested;
    private bool _searchCompletedOnce;
    private bool _busy;

    public EnrichissementPanel()
    {
        InitializeComponent();

        _processing = new ProcessingController(new ReconciliationEngine(new ApiClient()));

        StatusFilterCombo.ItemsSource = StatusFilters;
        StatusFilterCombo.SelectedIndex = 0;

        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = o => o is RowVm row && (SelectedStatus is null || row.Model.Status == SelectedStatus);
        ResultsGrid.ItemsSource = _view;

        _flushTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _flushTimer.Tick += (_, _) => FlushPendingResults();

        // Quitter l'écran (bouton Retour) interrompt la recherche en cours.
        Unloaded += (_, _) =>
        {
            _stopRequested = true;
            _flushTimer.Stop();
        };
    }

    private MatchStatus? SelectedStatus => (StatusFilterCombo.SelectedItem as StatusFilter)?.Status;

    private TierType SelectedTierType =>
        FournisseursRadio.IsChecked == true ? TierType.Fournisseur : TousRadio.IsChecked == true ? TierType.Tous : TierType.Client;

    private string DatabaseLabel => $"{DatabaseManager.Instance.CurrentInstance} / {DatabaseManager.Instance.CurrentDatabase}";

    private void SetBusy(bool busy)
    {
        _busy = busy;
        LaunchButton.IsEnabled = !busy;
        StopButton.IsEnabled = busy;
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        var canAct = _searchCompletedOnce && !_busy;
        ExportButton.IsEnabled = canAct;
        UpdateButton.IsEnabled = canAct;
    }

    private void StatusFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) _view.Refresh();
    }

    // ----- Recherche -----

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        _stopRequested = false;

        List<TierRecord> tiers;
        try
        {
            tiers = await _repository.FetchTiersAsync(SelectedTierType, ValidateExistingCheck.IsChecked == true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Enrichissement tiers : lecture des tiers impossible — {ex.Message}");
            MessageBox.Show($"Erreur lors de la lecture des tiers :\n{ex.Message}", "Enrichissement tiers", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _rows.Clear();
        SetBusy(true);
        Progress.Value = 0;
        Progress.Maximum = Math.Max(tiers.Count, 1);
        ProgressText.Text = $"Traitement : 0/{tiers.Count}";
        SummaryText.Text = "";
        Logger.Instance.Info($"Enrichissement tiers : recherche pour {tiers.Count} tiers ({DatabaseLabel}).");
        _flushTimer.Start();

        await _processing.RunAsync(
            tiers,
            onResult: result => _pendingResults.Enqueue(result),
            onProgress: (done, total) => Dispatcher.BeginInvoke(() =>
            {
                Progress.Value = done;
                ProgressText.Text = $"Traitement : {done}/{total}";
            }),
            isStopRequested: () => _stopRequested);

        FlushPendingResults();
        _flushTimer.Stop();

        _searchCompletedOnce = true;
        SetBusy(false);
        UpdateSummary();
        Logger.Instance.Success($"Enrichissement tiers : recherche {(_stopRequested ? "interrompue" : "terminée")} — {SummaryText.Text}");
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _stopRequested = true;

    private void FlushPendingResults()
    {
        if (_pendingResults.IsEmpty) return;

        while (_pendingResults.TryDequeue(out var result)) _rows.Add(new RowVm(result));
        UpdateSummary();
    }

    private void UpdateSummary() =>
        SummaryText.Text = string.Join(" | ", _rows
            .GroupBy(r => r.Model.Status)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()} {g.Key.ToDisplayString()}"));

    // ----- Tableau : sélection, double-clic, menu, tri -----

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not RowVm { HasCandidates: true } row) return;

        var dialog = new EnrichissementCandidateDialog(row.Model) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.SelectedCandidate is not null)
        {
            row.Model.ApplyCandidate(dialog.SelectedCandidate);
            row.Refresh();
            UpdateSummary();
        }
    }

    private void ResultsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || ResultsGrid.SelectedItems.Count == 0) return;

        foreach (var row in ResultsGrid.SelectedItems.OfType<RowVm>()) row.ToggleConfirmed();
        e.Handled = true;
    }

    private void ToggleConfirmed_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in ResultsGrid.SelectedItems.OfType<RowVm>()) row.ToggleConfirmed();
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in ResultsGrid.SelectedItems.OfType<RowVm>().ToList()) row.Ignore();
        _view.Refresh();
        UpdateSummary();
    }

    /// <summary>Tri par colonne : numérique si possible, sinon alphabétique ; ne casse pas le lien ligne/donnée.</summary>
    private void ResultsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (e.Column.SortMemberPath is not { Length: > 0 } path || _view is not ListCollectionView view) return;

        e.Handled = true;
        var direction = e.Column.SortDirection != ListSortDirection.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        view.CustomSort = new NumericOrAlphaComparer(path, direction);

        foreach (var column in ResultsGrid.Columns) column.SortDirection = null;
        e.Column.SortDirection = direction;
    }

    /// <summary>Compare deux lignes par une propriété : numériquement si les deux valeurs le permettent, sinon alphabétiquement.</summary>
    private sealed class NumericOrAlphaComparer(string propertyName, ListSortDirection direction) : IComparer
    {
        public int Compare(object? x, object? y)
        {
            var result = CompareValues(x?.GetType().GetProperty(propertyName)?.GetValue(x), y?.GetType().GetProperty(propertyName)?.GetValue(y));
            return direction == ListSortDirection.Descending ? -result : result;
        }

        private static int CompareValues(object? a, object? b)
        {
            if (a is bool ba && b is bool bb) return ba.CompareTo(bb);

            var sa = a?.ToString();
            var sb = b?.ToString();
            if (string.IsNullOrEmpty(sa) && string.IsNullOrEmpty(sb)) return 0;
            if (string.IsNullOrEmpty(sa)) return -1;
            if (string.IsNullOrEmpty(sb)) return 1;

            if (double.TryParse(sa, NumberStyles.Any, CultureInfo.InvariantCulture, out var da) &&
                double.TryParse(sb, NumberStyles.Any, CultureInfo.InvariantCulture, out var db))
                return da.CompareTo(db);

            return string.Compare(sa, sb, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    // ----- Export Excel -----

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exporter les lignes confirmées",
            Filter = "Classeur Excel (*.xlsx)|*.xlsx",
            FileName = "rapprochement_siret.xlsx",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            ExcelExporter.Export(_rows.Select(r => r.Model), dialog.FileName);
            Logger.Instance.Success($"Enrichissement tiers : export Excel réalisé : {dialog.FileName}");
            MessageBox.Show("Export Excel terminé.", "Enrichissement tiers", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Enrichissement tiers : export Excel impossible — {ex.Message}");
            MessageBox.Show($"Erreur lors de l'export :\n{ex.Message}", "Enrichissement tiers", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ----- Mise à jour Sage -----

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        var optionsDialog = new EnrichissementOptionsDialog { Owner = Window.GetWindow(this) };
        if (optionsDialog.ShowDialog() != true || optionsDialog.Result is not { } options) return;

        var confirmed = _rows.Where(r => r.Confirmed).ToList();
        if (confirmed.Count == 0)
        {
            MessageBox.Show("Aucune ligne confirmée à mettre à jour.", "Enrichissement tiers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"{confirmed.Count} fiche(s) vont être mises à jour dans la base {DatabaseManager.Instance.CurrentDatabase}. Continuer ?",
                "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        SetBusy(true);
        Progress.Value = 0;
        Progress.Maximum = confirmed.Count;
        ProgressText.Text = $"Mise à jour : 0/{confirmed.Count}";
        Logger.Instance.Info($"Enrichissement tiers : mise à jour de {confirmed.Count} fiche(s) ({DatabaseLabel})...");

        var updates = confirmed
            .Select(r =>
            {
                var (columns, audit) = SageUpdateBuilder.Build(r.Model, options);
                return (r.Model.Tier.CT_Num, columns, audit);
            })
            .ToList();

        List<UpdateResult> results;
        try
        {
            results = await _repository.ApplyUpdatesAsync(updates, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Enrichissement tiers : mise à jour impossible — {ex.Message}");
            SetBusy(false);
            MessageBox.Show($"Erreur lors de la mise à jour :\n{ex.Message}", "Enrichissement tiers", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _auditLogger.LogUpdates(DatabaseLabel, results);

        Progress.Value = Progress.Maximum;
        ProgressText.Text = $"Mise à jour : {confirmed.Count}/{confirmed.Count}";
        SetBusy(false);

        var successCount = results.Count(r => r.Success);
        var failures = results.Where(r => !r.Success).ToList();
        var message = $"{successCount}/{results.Count} fiche(s) mises à jour.\nJournal d'audit (valeurs avant / après) : {_auditLogger.FilePath}";
        if (failures.Count > 0)
        {
            message += "\n\nÉchecs :\n" + string.Join("\n", failures.Take(10).Select(f => $"  - {f.CtNum} : {f.ErrorMessage}"));
            if (failures.Count > 10) message += $"\n  ... et {failures.Count - 10} autre(s).";
        }

        foreach (var failure in failures) Logger.Instance.Error($"Enrichissement tiers : {failure.CtNum} non mis à jour — {failure.ErrorMessage}");
        Logger.Instance.Success($"Enrichissement tiers : {successCount}/{results.Count} fiche(s) mises à jour.");
        MessageBox.Show(message, "Enrichissement tiers", MessageBoxButton.OK, failures.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }
}
