using System.Windows;
using System.Windows.Controls;
using SageToolbox.Core;
using SageToolbox.Enrichissement;

namespace SageToolbox.Views;

/// <summary>Choix manuel de l'entreprise / de l'établissement quand l'API propose plusieurs candidats.</summary>
public partial class EnrichissementCandidateDialog : Window
{
    public ApiCandidate? SelectedCandidate { get; private set; }

    public EnrichissementCandidateDialog(ResultRow row)
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);

        SageInfoText.Text = $"{row.Tier.CT_Num} — {row.Tier.CT_Intitule}\n" +
                            $"{row.Tier.CT_Adresse} {row.Tier.CT_Complement} {row.Tier.CT_CodePostal} {row.Tier.CT_Ville}";
        CandidatesList.ItemsSource = row.Candidates;
    }

    private void Candidate_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: ApiCandidate candidate }) SelectedCandidate = candidate;
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is null)
        {
            MessageBox.Show("Veuillez sélectionner une entreprise.", "Enrichissement tiers", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
