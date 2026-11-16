using System.Windows;

namespace SageToolbox.Views;

/// <summary>Propriétés attachées des tuiles : <see cref="InstanceScopeProperty"/> affiche le badge « Instance »
/// sur un outil qui ne nécessite que la connexion à l'instance (pas de base sélectionnée) ;
/// <see cref="BetaProperty"/> affiche le badge « Bêta » sur une fonction encore en phase de test.</summary>
public static class Tile
{
    public static readonly DependencyProperty InstanceScopeProperty =
        DependencyProperty.RegisterAttached("InstanceScope", typeof(bool), typeof(Tile), new PropertyMetadata(false));

    public static readonly DependencyProperty BetaProperty =
        DependencyProperty.RegisterAttached("Beta", typeof(bool), typeof(Tile), new PropertyMetadata(false));

    public static bool GetBeta(DependencyObject element) => (bool)element.GetValue(BetaProperty);
    public static void SetBeta(DependencyObject element, bool value) => element.SetValue(BetaProperty, value);

    public static bool GetInstanceScope(DependencyObject element) => (bool)element.GetValue(InstanceScopeProperty);
    public static void SetInstanceScope(DependencyObject element, bool value) => element.SetValue(InstanceScopeProperty, value);
}
