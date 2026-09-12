using System.Windows;
using System.Windows.Input;
using SageToolbox.Core;
using SageToolbox.Models;
using SageToolbox.Repositories;

namespace SageToolbox.Views;

public partial class DevaliderFactureWindow : Window
{
    private readonly DocumentRepository _repository = new();
    private List<DocumentCandidate> _candidates = new();

    public DevaliderFactureWindow()
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);
        Loaded += (_, _) => PieceInput.Focus();
    }

    private async void Devalider_Click(object sender, RoutedEventArgs e)
    {
        // Si la liste de candidats est déjà affichée, on attend une sélection explicite.
        if (CandidatesList.Visibility == Visibility.Visible)
        {
            if (CandidatesList.SelectedItem is not DocumentCandidate selected)
            {
                MessageBox.Show("Merci de sélectionner le document à dévalider.", "Dévalider une facture",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            await ApplyAsync(selected);
            return;
        }

        var piece = PieceInput.Text.Trim();
        if (string.IsNullOrEmpty(piece))
        {
            Logger.Instance.Warning("Merci de saisir un numéro de pièce.");
            return;
        }

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            _candidates = await _repository.FindByPieceAsync(piece);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Recherche de la pièce {piece} : {ex.Message}");
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        switch (_candidates.Count)
        {
            case 0:
                MessageBox.Show($"Aucun document trouvé pour la pièce \"{piece}\".", "Dévalider une facture",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                break;
            case 1:
                await ApplyAsync(_candidates[0]);
                break;
            default:
                CandidatesList.ItemsSource = _candidates;
                CandidatesList.Visibility = Visibility.Visible;
                Logger.Instance.Info($"{_candidates.Count} documents partagent la pièce \"{piece}\" — sélectionnez celui à dévalider.");
                break;
        }
    }

    private async Task ApplyAsync(DocumentCandidate candidate)
    {
        if (!candidate.DoValide)
        {
            MessageBox.Show("Ce document est déjà en brouillon.", "Dévalider une facture",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Dévalider \"{candidate.Libelle}\" ?", "Confirmation",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Mouse.OverrideCursor = Cursors.Wait;
        IsEnabled = false;
        try
        {
            Logger.Instance.Info($"Dévalidation de la pièce {candidate.DoPiece}...");
            await _repository.DevaliderAsync(candidate);
            Logger.Instance.Success($"Pièce {candidate.DoPiece} dévalidée.");
            Close();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Dévalidation : échec — {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
        }
    }
}
