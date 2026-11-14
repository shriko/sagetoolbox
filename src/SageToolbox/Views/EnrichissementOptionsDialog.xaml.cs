using System.Windows;
using SageToolbox.Core;
using SageToolbox.Enrichissement;

namespace SageToolbox.Views;

/// <summary>Choix des champs de la fiche tiers à mettre à jour dans Sage 100.</summary>
public partial class EnrichissementOptionsDialog : Window
{
    public UpdateOptions? Result { get; private set; }

    public EnrichissementOptionsDialog()
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);
    }

    private void Adresse_CheckedChanged(object sender, RoutedEventArgs e) =>
        MajusculesCheckBox.IsEnabled = AdresseCheckBox.IsChecked == true;

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        Result = new UpdateOptions
        {
            UpdateNom = NomCheckBox.IsChecked == true,
            UpdateAdresse = AdresseCheckBox.IsChecked == true,
            AdresseEnMajuscules = MajusculesCheckBox.IsChecked == true,
            UpdateTva = TvaCheckBox.IsChecked == true,
            MettreEnSommeilSiRadie = SommeilCheckBox.IsChecked == true,
        };

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
