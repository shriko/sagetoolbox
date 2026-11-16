using System.Windows;
using SageToolbox.Core;
using SageToolbox.Recodification;

namespace SageToolbox.Views;

/// <summary>
/// Contrôles avant recodification : aucun autre utilisateur sur la base et sauvegarde récente (COPY_ONLY proposée si besoin).
/// En SPC, ni les sessions ni la sauvegarde SQL ne sont accessibles : les deux points sont confirmés par l'utilisateur.
/// </summary>
public partial class RecodificationPreflightDialog : Window
{
    private readonly RecodRepository _repository;
    private readonly DateTime? _lastApply;
    private readonly bool _spc = DatabaseManager.Instance.IsSpc;
    private bool _usersOk, _backupOk, _canCheckUsers = true;
    private BackupInfo? _backup;

    public RecodificationPreflightDialog(RecodRepository repository, DateTime? lastApplyServerTime, int count, string title)
    {
        InitializeComponent();
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);

        _repository = repository;
        _lastApply = lastApplyServerTime;
        var dossier = DatabaseManager.Instance.CurrentDatabase;
        Intro.Text = $"{count} code(s) à recodifier ({title}) dans {(_spc ? "la société" : "le dossier")} « {dossier} ».";

        if (_spc) SetupSpc();
        Loaded += async (_, _) =>
        {
            if (_spc)
            {
                UpdateRun();
                return;
            }

            BackupPath.Text = await DefaultBackupPathAsync();
            await CheckAsync();
        };
    }

    /// <summary>Texte repris dans le rapport de traitement.</summary>
    public string BackupDescription { get; private set; } = "";

    private void SetupSpc()
    {
        _canCheckUsers = false;
        UsersTitle.Text = "1. Aucun utilisateur sur la société";
        UsersStatus.Text = "Contrôle automatique impossible en SPC : la base Azure est partagée entre toutes les sociétés du dossier.";
        UsersConfirm.Content = "Je confirme qu'aucun utilisateur n'est connecté à cette société";
        UsersConfirm.Visibility = Visibility.Visible;
        RecheckUsersButton.Visibility = Visibility.Collapsed;

        BackupStatus.Text = "La sauvegarde SQL n'est pas disponible en SPC.";
        BackupPanel.Visibility = Visibility.Collapsed;
        BackupConfirm.Content = "Je confirme qu'une sauvegarde de la société a été réalisée avant ce traitement";
        BackupConfirm.Visibility = Visibility.Visible;
    }

    private async Task<string> DefaultBackupPathAsync()
    {
        var folder = (await _repository.GetDefaultBackupFolderAsync()).TrimEnd('\\');
        var file = $"{DatabaseManager.Instance.CurrentDatabase}_avant_recodification_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        return folder.Length > 0 ? $@"{folder}\{file}" : file;
    }

    private async Task CheckAsync()
    {
        Message.Text = "";
        try
        {
            var (canCheck, sessions) = await _repository.GetOtherSessionsAsync();
            _canCheckUsers = canCheck;
            if (!canCheck)
            {
                UsersStatus.Text = "Impossible de lister les sessions (droit VIEW SERVER STATE manquant).";
                UsersConfirm.Content = "Je confirme qu'aucun utilisateur n'est connecté à ce dossier (contrôle automatique impossible)";
                UsersConfirm.Visibility = Visibility.Visible;
                _usersOk = UsersConfirm.IsChecked == true;
            }
            else if (sessions.Count == 0)
            {
                UsersStatus.Text = "✔ Aucune autre session ouverte sur la base.";
                UsersConfirm.Visibility = Visibility.Collapsed;
                _usersOk = true;
            }
            else
            {
                UsersStatus.Text = "✘ Sessions ouvertes :\n" + string.Join("\n", sessions.Select(s => $"  • {s.Login} — {s.Host} — {s.Program}"));
                UsersConfirm.Visibility = Visibility.Collapsed;
                _usersOk = false;
            }

            _backup = await _repository.GetLastBackupAsync();
            _backupOk = PreflightRules.IsBackupValid(_backup, _lastApply);
            BackupStatus.Text = _backupOk
                ? $"✔ Sauvegarde du {_backup!.Finished:dd/MM/yyyy HH:mm} : {_backup.Device}"
                : _backup is null
                    ? "✘ Aucune sauvegarde complète enregistrée pour cette base."
                    : $"✘ Dernière sauvegarde : {_backup.Finished:dd/MM/yyyy HH:mm} — trop ancienne (plus de 24 h) ou antérieure à un traitement déjà appliqué.";
        }
        catch (Exception ex)
        {
            Message.Text = ex.Message;
            _usersOk = _backupOk = false;
        }
        UpdateRun();
    }

    private void UpdateRun() => RunButton.IsEnabled = _usersOk && _backupOk;

    private async void Recheck_Click(object sender, RoutedEventArgs e) => await CheckAsync();

    private void Confirm_Changed(object sender, RoutedEventArgs e)
    {
        if (!_canCheckUsers) _usersOk = UsersConfirm.IsChecked == true;
        if (_spc) _backupOk = BackupConfirm.IsChecked == true;
        UpdateRun();
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var path = BackupPath.Text.Trim();
        if (path.Length == 0) { Message.Text = "Indiquez le chemin du fichier de sauvegarde."; return; }
        BackupButton.IsEnabled = false;
        BackupStatus.Text = "Sauvegarde en cours…";
        try
        {
            await _repository.BackupAsync(path);
            await CheckAsync();
        }
        catch (Exception ex)
        {
            Message.Text = "Sauvegarde en échec : " + ex.Message;
            BackupStatus.Text = "✘ Sauvegarde non réalisée.";
        }
        finally
        {
            BackupButton.IsEnabled = true;
        }
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        BackupDescription = _spc
            ? "SPC : sauvegarde confirmée par l'utilisateur"
            : _backup is null ? "" : $"{_backup.Device} ({_backup.Finished:dd/MM/yyyy HH:mm})";
        DialogResult = true;
    }
}
