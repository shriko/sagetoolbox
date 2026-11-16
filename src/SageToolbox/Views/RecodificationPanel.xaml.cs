using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Recodification;

namespace SageToolbox.Views;

/// <summary>
/// Recodification en masse des comptes généraux, tiers, journaux, comptes analytiques, familles et articles : saisie ou import Excel
/// des nouveaux codes, contrôles, fusion éventuelle dans une fiche existante, puis traitement SQL transactionnel et rapport.
/// </summary>
public partial class RecodificationPanel : ToolPanel
{
    private sealed class ListState
    {
        public required List<RecodItem> Items { get; init; }
        public required List<(int Plan, string Code, int Nature)> AllKeys { get; init; }
        public required int MaxLength { get; init; }
        public required int FixedLength { get; init; }
    }

    private readonly RecodRepository _repository = new();
    private readonly Dictionary<ElementKind, ListState> _cache = new();
    private readonly DispatcherTimer _validationTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private ListState? _state;
    private ElementDefinition? _kind;
    private DateTime? _lastApplyServerTime;
    private bool _busy;

    public RecodificationPanel()
    {
        InitializeComponent();

        var db = DatabaseManager.Instance;
        DossierText.Text = db.IsSpc
            ? $"Société {db.CurrentDatabase} (schéma {db.CurrentSchema}) — {db.CurrentInstance}"
            : $"{db.CurrentDatabase} @ {db.CurrentInstance}";

        _validationTimer.Tick += (_, _) => { _validationTimer.Stop(); Revalidate(); };
        Unloaded += (_, _) => _validationTimer.Stop();
        Loaded += async (_, _) => await LoadKindsAsync();
    }

    private async Task LoadKindsAsync()
    {
        try
        {
            var hasGescom = await _repository.HasGescomAsync();
            KindCombo.ItemsSource = ElementDefinition.All.Where(d => hasGescom || !d.NeedsGescom).ToList();
            KindCombo.DisplayMemberPath = nameof(ElementDefinition.Title);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Erreur de lecture : " + ex.Message;
            Logger.Instance.Error($"Recodification : lecture impossible — {ex.Message}");
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        KindCombo.IsEnabled = !busy;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var ready = !_busy && _state is not null && _kind is not null;
        ExportButton.IsEnabled = ready;
        ImportButton.IsEnabled = ready;
        ApplyButton.IsEnabled = ready;
    }

    // ----- Chargement et validation -----

    private async void Kind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (KindCombo.SelectedItem is ElementDefinition def) await LoadAsync(def);
    }

    private async Task LoadAsync(ElementDefinition def, bool force = false)
    {
        _kind = def;
        PlanColumn.Visibility = def.HasPlan ? Visibility.Visible : Visibility.Collapsed;
        SetBusy(true);
        try
        {
            if (force || !_cache.ContainsKey(def.Kind))
            {
                StatusText.Text = "Lecture de la base…";
                var items = await _repository.LoadItemsAsync(def);
                _cache[def.Kind] = new ListState
                {
                    Items = items,
                    AllKeys = await _repository.LoadAllKeysAsync(def),
                    MaxLength = await _repository.GetKeyMaxLengthAsync(def),
                    FixedLength = def.Kind == ElementKind.ComptesGeneraux ? await _repository.GetFixedAccountLengthAsync() : 0,
                };
                foreach (var i in items) i.PropertyChanged += OnItemChanged;
            }

            _state = _cache[def.Kind];
            var view = CollectionViewSource.GetDefaultView(_state.Items);
            view.Filter = o => Matches((RecodItem)o);
            ItemsGrid.ItemsSource = view;
            Revalidate();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Erreur de lecture : " + ex.Message;
            Logger.Instance.Error($"Recodification : lecture de {def.Title} impossible — {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool Matches(RecodItem i)
    {
        var filter = FilterBox.Text;
        return filter.Length == 0
               || i.Code.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || i.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || i.NewCode.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void Filter_Changed(object sender, TextChangedEventArgs e)
    {
        if (ItemsGrid.ItemsSource is ICollectionView view) view.Refresh();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RecodItem.NewCode)) return;
        _validationTimer.Stop();
        _validationTimer.Start();
    }

    private ValidationSummary Revalidate()
    {
        if (_state is null) return new(0, 0);
        var s = RecodValidator.Validate(_state.Items, _state.AllKeys, _state.MaxLength, _state.FixedLength, _kind?.SupportsMerge == true);
        StatusText.Text = $"{_state.Items.Count} éléments — {s.ToRecode} à recodifier dont {s.Merges} fusion(s), {s.Errors} en erreur.";
        return s;
    }

    // ----- Excel -----

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_kind is null || _state is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "Exporter les fiches",
            Filter = "Classeur Excel (*.xlsx)|*.xlsx",
            FileName = $"{_kind.Title} - {DatabaseManager.Instance.CurrentDatabase}.xlsx"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            RecodExcel.Export(dialog.FileName, _kind, _state.Items);
            StatusText.Text = $"Export réalisé : {dialog.FileName}";
            Logger.Instance.Success($"Recodification : export Excel réalisé : {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export Excel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_kind is null || _state is null) return;
        var dialog = new OpenFileDialog { Title = "Importer les nouveaux codes", Filter = "Classeur Excel (*.xlsx)|*.xlsx" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var result = RecodExcel.Import(dialog.FileName, _kind, _state.Items);
            Revalidate();
            var message = $"{result.Matched} correspondance(s) importée(s).";
            if (result.Unknown.Count > 0)
                message += $"\n\n{result.Unknown.Count} code(s) du fichier introuvable(s) dans la base (ignorés) :\n" +
                           string.Join(", ", result.Unknown.Take(20)) + (result.Unknown.Count > 20 ? ", …" : "");
            MessageBox.Show(message, "Import Excel", MessageBoxButton.OK,
                result.Unknown.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import Excel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ----- Application -----

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_kind is null || _state is null) return;
        var def = _kind;
        _validationTimer.Stop();
        var summary = Revalidate();
        if (summary.Errors > 0)
        {
            MessageBox.Show($"{summary.Errors} ligne(s) en erreur : corrigez-les avant d'appliquer.", "Appliquer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (summary.ToRecode == 0)
        {
            MessageBox.Show("Aucun nouveau code saisi.", "Appliquer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var changes = _state.Items.Where(i => i.IsChanged).ToList();

        if (summary.Merges > 0 && MessageBox.Show(
                $"{summary.Merges} fiche(s) vont être FUSIONNÉES dans une fiche existante :\n" +
                "• la fiche de destination reste maître (intitulé, paramètres) ;\n" +
                "• tout le reste est reporté sur la destination ;\n" +
                "• la fiche source est supprimée définitivement.\n\nContinuer ?",
                "Fusion de fiches", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var preflight = new RecodificationPreflightDialog(_repository, _lastApplyServerTime, summary.ToRecode, def.Title)
        {
            Owner = Window.GetWindow(this)
        };
        if (preflight.ShowDialog() != true) return;

        var db = DatabaseManager.Instance;
        SetBusy(true);
        StatusText.Text = "Recodification en cours — ne fermez pas l'application…";
        Logger.Instance.Info($"Recodification : {def.Title}, {changes.Count} code(s) ({db.CurrentInstance} / {db.CurrentDatabase})...");
        try
        {
            var result = await Task.Run(() => RecodExecutor.RunAsync(_repository, def, changes));
            var report = RecodReport.Write(new ReportData(db.CurrentInstance!, db.IsSpc, db.CurrentDatabase!, db.CurrentSchema,
                def, changes, result, preflight.BackupDescription));

            if (result.Success)
            {
                _lastApplyServerTime = await _repository.GetServerTimeAsync();
                await LoadAsync(def, force: true);
                StatusText.Text = $"Recodification terminée ({changes.Count}). Rapport : {report}";
                Logger.Instance.Success($"Recodification : {def.Title} terminée, {changes.Count} code(s). Rapport : {report}");
            }
            else
            {
                StatusText.Text = "Échec : modifications annulées. Rapport : " + report;
                Logger.Instance.Error($"Recodification : {def.Title} en échec, modifications annulées — {result.Error}");
            }

            MessageBox.Show(
                result.Success
                    ? $"{changes.Count} code(s) recodifié(s).\n\nLe rapport va s'ouvrir.\n{report}"
                    : $"Échec, toutes les modifications ont été annulées.\n\n{result.Error}\n\nLe rapport va s'ouvrir.\n{report}",
                "Recodification", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            Process.Start(new ProcessStartInfo(report) { UseShellExecute = true });
        }
        finally
        {
            SetBusy(false);
        }
    }
}
