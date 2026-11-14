using System.Windows;

namespace SageToolbox.Views;

/// <summary>Propriétés attachées des tuiles : <see cref="InstanceScopeProperty"/> affiche le badge « Instance »
/// sur un outil qui ne nécessite que la connexion à l'instance (pas de base sélectionnée).</summary>
public static class Tile
{
    public static readonly DependencyProperty InstanceScopeProperty =
        DependencyProperty.RegisterAttached("InstanceScope", typeof(bool), typeof(Tile), new PropertyMetadata(false));

    public static bool GetInstanceScope(DependencyObject element) => (bool)element.GetValue(InstanceScopeProperty);
    public static void SetInstanceScope(DependencyObject element, bool value) => element.SetValue(InstanceScopeProperty, value);
}
