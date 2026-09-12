namespace SageToolbox.Models;

/// <summary>
/// Un exercice comptable du dossier (P_DOSSIER). Le numéro d'exercice (1-10, ou 1-5
/// en legacy) correspond au suffixe des champs D_DateDebutNN / D_DateFinNN / D_ArchivageNN
/// de P_DOSSIER (confirmé PDF Sage 100 "Structure des fichiers" p.18 : champs
/// D_Archivage01 à D_Archivage10, un par exercice — pas un champ générique).
/// </summary>
public sealed class ExerciceInfo
{
    public int Numero { get; init; }
    public DateTime DateDebut { get; init; }
    public DateTime DateFin { get; init; }
    public bool EstCloture { get; init; }

    public string Libelle =>
        $"Exercice {Numero} : {DateDebut:dd/MM/yyyy} - {DateFin:dd/MM/yyyy} ({(EstCloture ? "Clôturé" : "Ouvert")})";
}
