namespace SageToolbox.Models;

public sealed class SageDatabaseInfo
{
    public string DatabaseName { get; init; } = string.Empty;
    public bool HasComptabilite { get; init; }
    public bool HasGestionCommerciale { get; init; }
    public bool HasTresorerie { get; init; }
    public bool HasImmobilisations { get; init; }
    public double DataSizeMb { get; init; }
    public double LogSizeMb { get; init; }
    public double FileStreamSizeMb { get; init; }
    public string PhysicalPath { get; init; } = string.Empty;

    public IEnumerable<string> InstalledProducts()
    {
        if (HasComptabilite) yield return "Comptabilité";
        if (HasGestionCommerciale) yield return "Gestion Commerciale";
        if (HasTresorerie) yield return "Trésorerie";
        if (HasImmobilisations) yield return "Immobilisations";
    }
}
