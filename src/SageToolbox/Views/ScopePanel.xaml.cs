using System.Windows;
using SageToolbox.Core;

namespace SageToolbox.Views;

/// <summary>
/// Choix de la portée d'un outil disponible à deux niveaux : la base connectée ou toutes les
/// bases Sage de l'instance. Sans base sélectionnée, seule la portée « toutes les bases » reste possible.
/// </summary>
public partial class ScopePanel : ToolPanel
{
    public ScopePanel(string title, string description, string actionLabel)
    {
        InitializeComponent();
        Title = title;
        DescriptionText.Text = description;
        RunButton.Content = actionLabel;

        var database = DatabaseManager.Instance.CurrentDatabase;
        DatabaseRadio.Content = database is null
            ? "La base connectée (aucune base sélectionnée)"
            : $"La base connectée : {database}";
        DatabaseRadio.IsEnabled = database is not null;
        (database is null ? AllRadio : DatabaseRadio).IsChecked = true;
    }

    /// <summary>Déclenché avec true pour « toutes les bases », false pour la base connectée.</summary>
    public event EventHandler<bool>? ScopeChosen;

    private void Run_Click(object sender, RoutedEventArgs e) =>
        ScopeChosen?.Invoke(this, AllRadio.IsChecked == true);
}
