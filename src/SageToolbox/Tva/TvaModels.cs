using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SageToolbox.Tva;

/// <summary>Base Sage Comptabilité détectée sur le serveur (ligne de la grille de sélection).</summary>
public sealed class BaseInfo : INotifyPropertyChanged
{
    private static readonly System.Globalization.CultureInfo Fr = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
    private bool _selected;

    public required string Nom { get; init; }
    public required string Societe { get; init; }
    public decimal TailleMo { get; init; }
    public int NbEcrituresN { get; init; }
    public int NbEcrituresN1 { get; init; }

    public string TailleLabel => TailleMo.ToString("N2", Fr);
    public string NbEcrituresNLabel => NbEcrituresN.ToString("N0", Fr);
    public string NbEcrituresN1Label => NbEcrituresN1.ToString("N0", Fr);

    public bool Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Définition d'un contrôle d'audit : un script SQL exécuté sur chaque base.</summary>
/// <param name="AnomalyColumn">Colonne dont une valeur non vide (et différente de « Non ») signale une anomalie.</param>
/// <param name="Transposed">Résultat d'une seule ligne, présenté en deux colonnes (paramètre / valeur).</param>
public sealed record ControlDefinition(
    string Id,
    string Title,
    string Description,
    string Sql,
    string? AnomalyColumn = null,
    bool Transposed = false);

public sealed class ControlResult
{
    public required ControlDefinition Definition { get; init; }
    public string[] Columns { get; set; } = [];
    public List<string[]> Rows { get; } = [];
    public HashSet<int> AnomalyRows { get; } = [];
    public string? Error { get; set; }

    public int AnomalyCount => AnomalyRows.Count;
}

public sealed class BaseAnalysis
{
    public required BaseInfo Info { get; init; }
    public List<ControlResult> Results { get; } = [];

    public int TotalAnomalies => Results.Sum(r => r.AnomalyCount);
    public int TotalErrors => Results.Count(r => r.Error is not null);
}

public sealed class ReportModel
{
    public required string Title { get; init; }
    public required string Server { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public required IReadOnlyList<BaseAnalysis> Bases { get; init; }
}

[Flags]
public enum ReportFormats
{
    None = 0,
    Word = 1,
    Pdf = 2,
    Excel = 4
}
