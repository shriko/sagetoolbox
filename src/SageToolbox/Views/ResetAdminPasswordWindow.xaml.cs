using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SageToolbox.Core;
using SageToolbox.Repositories;

namespace SageToolbox.Views;

public partial class ResetAdminPasswordWindow : Window
{
    private readonly AdminRepository _repository = new();
    private readonly List<CheckBox> _optionCheckBoxes = new();

    public ResetAdminPasswordWindow()
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);

        foreach (var option in AdminRepository.SecurityOptions)
        {
            var checkBox = new CheckBox
            {
                Content = option.Label,
                Tag = option.Column,
                IsChecked = true,
                Margin = new Thickness(0, 4, 0, 4)
            };
            _optionCheckBoxes.Add(checkBox);
            OptionsPanel.Children.Add(checkBox);
        }
    }

    private async void Reinitialiser_Click(object sender, RoutedEventArgs e)
    {
        var columnsToDisable = _optionCheckBoxes
            .Where(c => c.IsChecked == true)
            .Select(c => (string)c.Tag)
            .ToList();

        if (MessageBox.Show(
            "Voulez-vous vraiment réinitialiser le mot de passe du compte <Administrateur> ?",
            "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Mouse.OverrideCursor = Cursors.Wait;
        IsEnabled = false;
        try
        {
            Logger.Instance.Info("Réinitialisation du mot de passe administrateur...");
            var affected = await _repository.ResetAdminPasswordAsync(columnsToDisable);
            if (affected.Count == 0)
                Logger.Instance.Warning("Aucun logiciel Sage concerné trouvé pour la réinitialisation.");
            else
                Logger.Instance.Success($"Mot de passe administrateur réinitialisé pour : {string.Join(" / ", affected)}");

            if (columnsToDisable.Count > 0)
                Logger.Instance.Info($"Options de gestion du mot de passe désactivées : {columnsToDisable.Count}.");

            Close();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Réinitialisation du mot de passe administrateur : échec — {ex.Message}");
            MessageBox.Show(ex.Message, "Réinitialisation impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
        }
    }
}
