using System.ComponentModel;

namespace SageToolbox.Recodification;

/// <summary>Une fiche recodifiable (ligne du tableau) : code actuel, intitulé et nouveau code saisi.</summary>
public sealed class RecodItem : INotifyPropertyChanged
{
    private string _newCode = "";
    private string? _error;
    private string? _info;

    public RecodItem(int plan, string code, string label)
    {
        Plan = plan;
        Code = code;
        Label = label;
    }

    /// <summary>Numéro de plan analytique (0 pour les autres types).</summary>
    public int Plan { get; }
    public string Code { get; }
    public string Label { get; }

    public string NewCode
    {
        get => _newCode;
        set
        {
            if (_newCode == value) return;
            _newCode = value ?? "";
            Raise(nameof(NewCode));
        }
    }

    public string? Error
    {
        get => _error;
        set
        {
            if (_error == value) return;
            _error = value;
            Raise(nameof(Error));
            Raise(nameof(Message));
        }
    }

    public string? Info
    {
        get => _info;
        set
        {
            if (_info == value) return;
            _info = value;
            Raise(nameof(Info));
            Raise(nameof(Message));
        }
    }

    /// <summary>Le nouveau code désigne une fiche existante : l'ancienne est fusionnée dans celle-ci.</summary>
    public bool IsMerge { get; set; }

    /// <summary>Code de la fiche de destination tel qu'il est enregistré dans la base (casse d'origine).</summary>
    public string? MergeTarget { get; set; }

    public string Message => Error ?? Info ?? "";

    public string NewCodeTrimmed => NewCode.Trim();

    /// <summary>Code réellement appliqué : la fiche de destination en cas de fusion, sinon la saisie.</summary>
    public string EffectiveNewCode => IsMerge ? MergeTarget ?? NewCodeTrimmed : NewCodeTrimmed;

    /// <summary>Vrai si une nouvelle valeur différente de l'ancienne est saisie.</summary>
    public bool IsChanged => NewCodeTrimmed.Length > 0 && !string.Equals(NewCodeTrimmed, Code, StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
