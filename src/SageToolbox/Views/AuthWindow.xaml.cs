using System.Windows;
using System.Windows.Input;
using SageToolbox.Core;

namespace SageToolbox.Views;

public partial class AuthWindow : Window
{
    public AuthWindow()
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);
        Loaded += (_, _) => PasswordInput.Focus();

        // Cette fenêtre est la toute première de l'application (avant tout autre rendu
        // WPF) : sur certaines sessions (notamment via Bureau à distance), la toute
        // première image affichée peut être peinte avant que la mise en page ne soit
        // complètement calculée, laissant apparaître le fond système par défaut (blanc)
        // au lieu du fond thémé. Un UpdateLayout() explicite avant affichage force le
        // calcul complet en amont du premier paint.
        UpdateLayout();
    }

    private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Validate_Click(sender, e);
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordInput.Password;
        if (PasswordHasher.Verify(password))
        {
            DialogResult = true;
            Close();
        }
        else
        {
            ErrorText.Text = "Mot de passe incorrect.";
            ErrorText.Visibility = Visibility.Visible;
            PasswordInput.Clear();
            PasswordInput.Focus();
        }
    }
}
