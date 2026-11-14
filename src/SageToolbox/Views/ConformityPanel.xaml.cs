using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Schematron;

namespace SageToolbox.Views;

/// <summary>Contrôle de conformité d'une facture électronique (UBL, CII, Factur-X, CDAR) aux schematrons France_RFE.</summary>
public partial class ConformityPanel : ToolPanel
{
    /// <summary>Ligne de la liste des règles ; <see cref="IsExpanded"/> déplie le détail au clic.</summary>
    public sealed class RuleVm : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public RuleVm(RuleResult rule)
        {
            Rule = rule;
            Id = rule.Id;
            Message = rule.Message;
            Ruleset = rule.RulesetLabel;
            Context = rule.Context;
            Test = rule.Test;
            Status = rule.Status;
            IsFatal = rule.Flag == "fatal";
            FlagLabel = rule.Status == RuleResult.Failed ? (IsFatal ? "Bloquant" : "Avertissement") : "";
            StatusIcon = rule.Status switch { RuleResult.Failed => "✗", RuleResult.Passed => "✓", _ => "–" };
            Occurrences = OccurrenceLabel(rule);
            FailuresText = string.Join("\n\n", rule.Failures.Select(f => $"{f.LocationSimple}\n{f.Text}"));
        }

        public RuleResult Rule { get; }
        public string Id { get; }
        public string Message { get; }
        public string Ruleset { get; }
        public string Context { get; }
        public string Test { get; }
        public string Status { get; }
        public bool IsFatal { get; }
        public string FlagLabel { get; }
        public string StatusIcon { get; }
        public string Occurrences { get; }
        public string FailuresText { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private static string OccurrenceLabel(RuleResult rule)
        {
            if (rule.Status == RuleResult.NotApplicable) return "Non applicable à ce document (élément concerné absent).";
            var total = rule.OccurrencesTotal == 1 ? "1 occurrence contrôlée" : $"{rule.OccurrencesTotal} occurrences contrôlées";
            return rule.Status == RuleResult.Failed ? $"{total} — {rule.Failures.Count} en échec." : $"{total} — toutes conformes.";
        }
    }

    public sealed record VigilanceVm(string Heading, string Message, bool IsCritical);

    private readonly ObservableCollection<RuleVm> _rules = new();
    private readonly ICollectionView _rulesView;
    private string? _filePath;
    private ConformityReport? _report;

    public ConformityPanel()
    {
        InitializeComponent();

        ProfileCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Détection automatique", Tag = null });
        foreach (var profile in Rulesets.Profiles.Values)
            ProfileCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = profile.Label, Tag = profile.Key, ToolTip = profile.Description });
        ProfileCombo.SelectedIndex = 0;

        _rulesView = CollectionViewSource.GetDefaultView(_rules);
        _rulesView.Filter = o => o is RuleVm rule && Matches(rule);
        RulesList.ItemsSource = _rulesView;
    }

    // ----- Sélection du fichier -----

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Facture électronique à contrôler",
            Filter = "Factures électroniques (*.xml;*.pdf)|*.xml;*.pdf|Tous les fichiers (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true) SetFile(dialog.FileName);
    }

    private void Panel_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Panel_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) SetFile(files[0]);
    }

    private void SetFile(string path)
    {
        _filePath = path;
        FileNameText.Text = path;
        ValidateButton.IsEnabled = true;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    // ----- Contrôle -----

    private async void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (_filePath is null) return;

        var path = _filePath;
        var profileKey = (ProfileCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;

        ValidateButton.IsEnabled = false;
        BusyPanel.Visibility = Visibility.Visible;
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            var (report, fromPdf) = await Task.Run(() =>
            {
                var data = File.ReadAllBytes(path);
                var isPdf = FacturXExtractor.IsPdf(data);
                if (isPdf) data = FacturXExtractor.ExtractXml(data);
                return (SchematronEngine.Validate(data, profileKey), isPdf);
            });
            ShowReport(report, Path.GetFileName(path), fromPdf);
        }
        catch (ConformityException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            ShowError($"Contrôle impossible : {ex.Message}");
        }
        finally
        {
            BusyPanel.Visibility = Visibility.Collapsed;
            ValidateButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        Logger.Instance.Error($"Conformité e-facture : {message}");
    }

    private void ShowReport(ConformityReport report, string fileName, bool fromPdf)
    {
        _report = report;

        BadgeText.Text = report.Conforme ? "✓ CONFORME" : "✗ NON CONFORME";
        BadgeText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, report.Conforme ? "SuccessBrush" : "DangerBrush");

        var document = new StringBuilder($"{fileName} — format {report.FormatLabel}");
        if (fromPdf) document.Append(" (XML extrait du PDF Factur-X)");
        document.Append($" — élément racine <{report.RootElement}>");
        if (report.CustomizationId.Length > 0) document.Append($" — identifiant de spécification : {report.CustomizationId}");
        document.Append($" — profil appliqué : {report.Profile.Label}");
        DocumentText.Text = document.ToString();

        CountsText.Inlines.Clear();
        AddCount($"{report.FatalCount} erreur(s) bloquante(s)", "DangerBrush");
        AddCount($"{report.XsdErrorCount} erreur(s) XSD", "DangerBrush");
        AddCount($"{report.WarningCount} avertissement(s)", "WarningBrush");
        AddCount($"{report.PassedCount} règle(s) conforme(s)", "SuccessBrush");
        CountsText.Inlines.Add(new Run($"{report.NotApplicableCount} non applicable(s) sur {report.EvaluatedCount + report.NotApplicableCount} règles du profil"));
        SummaryBorder.Visibility = Visibility.Visible;

        var vigilance = new List<VigilanceVm>();
        if (report.Xsd is { Valid: false } xsd)
        {
            var lines = xsd.Errors.Take(20).Select(x => $"ligne {x.Line} — {x.Message}").ToList();
            if (xsd.Errors.Count > 20) lines.Add($"… et {xsd.Errors.Count - 20} autre(s) erreur(s).");
            vigilance.Add(new VigilanceVm($"⛔ Point de vigilance critique — Structure XML non conforme au schéma XSD ({xsd.Errors.Count} erreur(s))",
                $"Schéma {xsd.Schema}\n" + string.Join("\n", lines), true));
        }
        vigilance.AddRange(report.VigilancePoints.Select(p => p.Level == "critical"
            ? new VigilanceVm($"⛔ Point de vigilance critique — {p.Title}", p.Message, true)
            : new VigilanceVm($"⚠ Point de vigilance — {p.Title}", p.Message, false)));
        VigilanceList.ItemsSource = vigilance;

        // À chaque nouveau contrôle : n'afficher que les règles en échec.
        SearchBox.Text = "";
        ShowFailedCheck.IsChecked = true;
        ShowPassedCheck.IsChecked = false;
        ShowNaCheck.IsChecked = false;

        _rules.Clear();
        foreach (var rule in report.Rules) _rules.Add(new RuleVm(rule));
        FilterBar.Visibility = Visibility.Visible;
        RefreshRules();

        Logger.Instance.Info($"Conformité e-facture : {fileName} — {(report.Conforme ? "CONFORME" : "NON CONFORME")} " +
                             $"({report.FatalCount} erreur(s) bloquante(s), {report.XsdErrorCount} erreur(s) XSD, {report.WarningCount} avertissement(s)).");
    }

    private void AddCount(string text, string brushKey)
    {
        var run = new Run(text);
        run.SetResourceReference(TextElement.ForegroundProperty, brushKey);
        CountsText.Inlines.Add(run);
        CountsText.Inlines.Add(new Run(" · "));
    }

    // ----- Filtres et dépliage des règles -----

    private bool Matches(RuleVm rule)
    {
        var statusShown = rule.Status switch
        {
            RuleResult.Failed => ShowFailedCheck.IsChecked == true,
            RuleResult.Passed => ShowPassedCheck.IsChecked == true,
            _ => ShowNaCheck.IsChecked == true,
        };
        if (!statusShown) return false;

        var query = SearchBox.Text.Trim();
        return query.Length == 0
            || rule.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
            || rule.Message.Contains(query, StringComparison.OrdinalIgnoreCase)
            || rule.Context.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => RefreshRules();

    private void RefreshRules()
    {
        _rulesView.Refresh();
        var any = !_rulesView.IsEmpty;
        RulesList.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        if (any) return;

        EmptyText.Text = _report is { FailedCount: 0 } && ShowFailedCheck.IsChecked == true && SearchBox.Text.Trim().Length == 0
            ? "✓ Aucune règle en échec : toutes les règles applicables du profil sont respectées."
            : "Aucune règle ne correspond aux filtres sélectionnés.";
    }

    private void Rule_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RuleVm rule }) rule.IsExpanded = !rule.IsExpanded;
    }
}
