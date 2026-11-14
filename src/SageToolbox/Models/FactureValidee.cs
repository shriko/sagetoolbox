using System.ComponentModel;
using System.Globalization;

namespace SageToolbox.Models;

/// <summary>
/// Facture de vente validée (F_DOCENTETE, DO_Type 6, DO_Valide 1) candidate à la dévalidation.
/// Une facture déjà transmise en facturation électronique n'est pas sélectionnable.
/// </summary>
public sealed class FactureValidee : INotifyPropertyChanged
{
    public const string MotifEnvoiElectronique =
        "La facture a été envoyée électroniquement, elle existe côté Administration Fiscale, dévalidation impossible, annuler et faire une nouvelle facture.";

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private bool _isChecked;

    public int DoType { get; init; }
    public string DoPiece { get; init; } = string.Empty;
    public DateTime DoDate { get; init; }
    public string CtNum { get; init; } = string.Empty;
    public string CtIntitule { get; init; } = string.Empty;
    public decimal TotalHt { get; init; }
    public bool EnvoyeeElectroniquement { get; init; }

    public string DateLabel => DoDate.ToString("dd/MM/yyyy", Fr);
    public string TotalHtLabel => TotalHt.ToString("N2", Fr);
    public bool IsBlocked => EnvoyeeElectroniquement;
    public string BlockReason => IsBlocked ? MotifEnvoiElectronique : string.Empty;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
