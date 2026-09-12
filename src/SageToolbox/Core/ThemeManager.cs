using System.Windows;
using Microsoft.Win32;

namespace SageToolbox.Core;

public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>
/// Bascule de thème Système / Clair / Sombre en échangeant le ResourceDictionary
/// de couleurs fusionné dans App.Resources.
/// </summary>
public static class ThemeManager
{
    /// <summary>Thème effectif actuellement appliqué (résolu, jamais "Système") — lu par
    /// chaque fenêtre pour thémer sa barre de titre via WindowChromeHelper.</summary>
    public static AppTheme CurrentEffectiveTheme { get; private set; } = AppTheme.Light;

    public static void Apply(AppTheme theme)
    {
        var effective = theme == AppTheme.System ? DetectSystemTheme() : theme;
        CurrentEffectiveTheme = effective;
        var uri = effective == AppTheme.Dark
            ? new Uri("Views/Themes/Dark.xaml", UriKind.Relative)
            : new Uri("Views/Themes/Light.xaml", UriKind.Relative);

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d =>
            d.Source is not null &&
            (d.Source.OriginalString.EndsWith("Light.xaml") || d.Source.OriginalString.EndsWith("Dark.xaml")));
        var newDict = new ResourceDictionary { Source = uri };

        if (existing is not null)
        {
            var index = dictionaries.IndexOf(existing);
            dictionaries[index] = newDict;
        }
        else
        {
            dictionaries.Insert(0, newDict);
        }

        ConfigManager.Instance.Config.Theme = theme.ToString();
        ConfigManager.Instance.Save();
    }

    private static AppTheme DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                return i == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            // Registre inaccessible -> on retombe sur le thème clair par défaut.
        }
        return AppTheme.Light;
    }

    public static AppTheme ParseSaved(string? value) =>
        Enum.TryParse<AppTheme>(value, out var theme) ? theme : AppTheme.System;
}
