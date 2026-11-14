using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Tva;
using SageToolbox.Tva.Reports;

namespace SageToolbox.Views;

public partial class AuditTvaPanel : ToolPanel
{
    private readonly ObservableCollection<BaseInfo> _bases = new();
    private readonly string _instance = DatabaseManager.Instance.CurrentInstance!;
    private CancellationTokenSource? _cts;

    public AuditTvaPanel()
    {
        InitializeComponent();
        BasesList.ItemsSource = _bases;
        OutputInput.Text = ConfigManager.Instance.Config.LastTvaOutputFolder
            ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        Loaded += async (_, _) => await LoadBasesAsync();
        Unloaded += (_, _) => _cts?.Cancel();
    }

    private void SetBusy(bool busy)
    {
        RefreshButton.IsEnabled = !busy;
        AnalyzeButton.IsEnabled = !busy && _bases.Count > 0;
        CancelButton.IsEnabled = busy;
        BasesList.IsEnabled = !busy;
    }

    private void UpdateCount() =>
        CountText.Text = $"{_bases.Count(b => b.Selected)} base(s) sélectionnée(s) sur {_bases.Count}";

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadBasesAsync();

    private async Task LoadBasesAsync()
    {
        _cts = new CancellationTokenSource();
        SetBusy(true);
        Progress.IsIndeterminate = true;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            Logger.Instance.Info($"Audit TVA : recherche des bases Sage Comptabilité sur {_instance}...");
            var list = await Task.Run(() => new TvaSqlService(_instance).ListBasesAsync(_cts.Token));

            _bases.Clear();
            var current = DatabaseManager.Instance.CurrentDatabase;
            foreach (var b in list)
            {
                b.PropertyChanged += (_, _) => UpdateCount();
                b.Selected = string.Equals(b.Nom, current, StringComparison.OrdinalIgnoreCase);
                _bases.Add(b);
            }
            UpdateCount();
            Logger.Instance.Success($"Audit TVA : {list.Count} base(s) Sage Comptabilité trouvée(s).");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Audit TVA : liste des bases impossible — {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
            Progress.IsIndeterminate = false;
            SetBusy(false);
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) { foreach (var b in _bases) b.Selected = true; }
    private void SelectNone_Click(object sender, RoutedEventArgs e) { foreach (var b in _bases) b.Selected = false; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Dossier de sortie des rapports", InitialDirectory = OutputInput.Text };
        if (dialog.ShowDialog() == true) OutputInput.Text = dialog.FolderName;
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        var selected = _bases.Where(b => b.Selected).ToList();
        var formats = (WordCheck.IsChecked == true ? ReportFormats.Word : 0)
                    | (PdfCheck.IsChecked == true ? ReportFormats.Pdf : 0)
                    | (ExcelCheck.IsChecked == true ? ReportFormats.Excel : 0);
        var outputDir = OutputInput.Text.Trim();

        if (selected.Count == 0)
        {
            MessageBox.Show("Sélectionnez au moins une base.", "Audit TVA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (formats == ReportFormats.None)
        {
            MessageBox.Show("Choisissez au moins un format de rapport.", "Audit TVA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (outputDir.Length == 0)
        {
            MessageBox.Show("Renseignez le dossier de sortie.", "Audit TVA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConfigManager.Instance.Config.LastTvaOutputFolder = outputDir;
        ConfigManager.Instance.Save();

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var onePerBase = PerBaseRadio.IsChecked == true;

        SetBusy(true);
        Progress.Value = 0;
        Progress.Maximum = selected.Count;
        Logger.Instance.Info($"Audit TVA : analyse de {selected.Count} base(s)...");

        var progress = new Progress<string>(message => Logger.Instance.Info(message));
        try
        {
            var service = new TvaSqlService(_instance);
            var analyses = new List<BaseAnalysis>();

            foreach (var b in selected)
            {
                ct.ThrowIfCancellationRequested();
                Logger.Instance.Info($"-> {b.Nom} ({b.Societe})");
                try
                {
                    analyses.Add(await Task.Run(() => service.AnalyseAsync(b, progress, ct), ct));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.Instance.Error($"   Base ignorée : {ex.Message}");
                }
                Progress.Value++;
            }

            if (analyses.Count == 0)
            {
                Logger.Instance.Warning("Audit TVA : aucune base analysée, aucun rapport généré.");
                return;
            }

            Logger.Instance.Info("Audit TVA : génération du rapport...");
            var files = await Task.Run(() => ReportService.Generate(analyses, _instance, outputDir, formats, onePerBase));
            foreach (var file in files) Logger.Instance.Success($"Rapport généré : {file}");

            var total = analyses.Sum(a => a.TotalAnomalies);
            Logger.Instance.Success($"Audit TVA terminé : {total} anomalie(s), {files.Count} fichier(s) généré(s).");
            if (MessageBox.Show($"Analyse terminée : {total} anomalie(s), {files.Count} fichier(s) généré(s).\n\nOuvrir le dossier ?",
                "Audit TVA", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(outputDir) { UseShellExecute = true });
        }
        catch (OperationCanceledException) { Logger.Instance.Warning("Audit TVA : analyse annulée."); }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Audit TVA : échec — {ex.Message}");
            MessageBox.Show(ex.Message, "Audit TVA", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }
}
