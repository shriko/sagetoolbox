using System.ComponentModel;

namespace SageToolbox.Rfe;

public sealed class RfeContact
{
    public string Compte { get; init; } = "";
    public string Service { get; init; } = "";
    public string Contact { get; init; } = "";
    public string Fonction { get; init; } = "";
    public string Telephone { get; init; } = "";
    public string Email { get; init; } = "";

    public string DisplayName
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(Fonction) ? Contact : $"{Contact} ({Fonction})";
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(Telephone)) details.Add($"Tél. {Telephone}");
            if (!string.IsNullOrWhiteSpace(Email)) details.Add(Email);
            return details.Count == 0 ? label : $"{label} — {string.Join(" / ", details)}";
        }
    }
}

/// <summary>Une adresse de facturation électronique (maille de routage) d'un client Sage.</summary>
public sealed class RfeAddress : INotifyPropertyChanged
{
    private RfeContact? _selectedContact;

    public string Compte { get; init; } = "";
    public string RaisonSociale { get; init; } = "";
    public string Telephone { get; init; } = "";
    public string Email { get; init; } = "";
    public string TypeMaille { get; init; } = "";
    public string MailleAdresse { get; init; } = "";
    public string Actif { get; init; } = "";
    public int NbAdresses { get; init; }

    public List<RfeContact> Contacts { get; set; } = new();

    public bool HasContacts => Contacts.Count > 0;

    public RfeContact? SelectedContact
    {
        get => _selectedContact;
        set
        {
            if (ReferenceEquals(_selectedContact, value)) return;
            _selectedContact = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedContact)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
