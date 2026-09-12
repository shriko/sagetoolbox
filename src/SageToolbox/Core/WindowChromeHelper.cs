using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SageToolbox.Core;

/// <summary>
/// La barre de titre et le contour de fenêtre (zone non-client) sont dessinés par
/// Windows, hors du contrôle de WPF : sans ces appels DWM, ils restent clairs même en
/// thème sombre. DWMWA_USE_IMMERSIVE_DARK_MODE seul ne teinte que la barre de titre sur
/// certaines versions de Windows 11 et laisse le contour (bordure fine autour de la
/// fenêtre) dans une couleur claire/accent par défaut — d'où l'ajout explicite de
/// DWMWA_BORDER_COLOR/CAPTION_COLOR/TEXT_COLOR (Windows 11 22H2+ ; ignorés silencieusement
/// sur les versions antérieures, DwmSetWindowAttribute renvoie alors un code d'erreur
/// sans lever d'exception).
/// </summary>
public static class WindowChromeHelper
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int valueSize);

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const uint DwmwaColorDefault = 0xFFFFFFFF;

    // Couleurs alignées sur Views/Themes/Dark.xaml (WindowBackgroundBrush, CardBorderBrush, TextPrimaryBrush).
    private static readonly uint DarkCaption = PackColor(0x18, 0x18, 0x1B);
    private static readonly uint DarkBorder = PackColor(0x3F, 0x3F, 0x46);
    private static readonly uint DarkText = PackColor(0xFA, 0xFA, 0xFA);

    public static void ApplyTitleBarTheme(Window window, bool dark)
    {
        void Apply()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            var darkMode = (uint)(dark ? 1 : 0);
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(uint)) != 0)
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref darkMode, sizeof(uint));

            var caption = dark ? DarkCaption : DwmwaColorDefault;
            var border = dark ? DarkBorder : DwmwaColorDefault;
            var text = dark ? DarkText : DwmwaColorDefault;
            DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(uint));
            DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(uint));
            DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(uint));
        }

        if (PresentationSource.FromVisual(window) is not null)
            Apply();
        else
            window.SourceInitialized += (_, _) => Apply();
    }

    /// <summary>Empaquette RGB en COLORREF (0x00BBGGRR) attendu par les attributs DWM.</summary>
    private static uint PackColor(byte r, byte g, byte b) => (uint)((b << 16) | (g << 8) | r);
}
