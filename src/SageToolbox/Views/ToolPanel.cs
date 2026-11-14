using System.Windows;
using System.Windows.Controls;

namespace SageToolbox.Views;

/// <summary>
/// Base des écrans d'outils affichés dans la zone centrale de la fenêtre principale.
/// <see cref="Title"/> alimente le titre sous le bouton Retour ; <see cref="Close"/> ramène
/// à la grille des tuiles (équivalent de l'ancien Window.Close()). Par défaut un panneau est
/// un formulaire compact (560 px, en haut à gauche) ; un écran à liste le surcharge en XAML.
/// </summary>
public class ToolPanel : UserControl
{
    public ToolPanel()
    {
        MaxWidth = 560;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    public string Title { get; set; } = string.Empty;

    public event EventHandler? CloseRequested;

    protected void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
