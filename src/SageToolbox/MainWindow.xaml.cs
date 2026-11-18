using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SageToolbox.Core;
using SageToolbox.Models;
using SageToolbox.Repositories;
using SageToolbox.Views;

namespace SageToolbox;

public partial class MainWindow : Window
{
    private readonly ServerRepository _serverRepository = new();
    private readonly ConnectionFileRepository _connectionFileRepository = new();
    private readonly IntegrityRepository _integrityRepository = new();
    private readonly CommercialIntegrityRepository _commercialIntegrityRepository = new();
    private readonly TiersRepository _tiersRepository = new();
    private readonly AdminRepository _adminRepository = new();
    private readonly ObservableCollection<LogEntry> _logEntries = new();

    /// <summary>Boutons nécessitant une base précisément sélectionnée.</summary>
    private readonly List<Button> _databaseButtons = new();

    /// <summary>Boutons "Serveur &amp; Instance", qui ne nécessitent que l'instance
    /// (utilisables même en connexion instance seule, sans base sélectionnée).</summary>
    private readonly List<Button> _instanceButtons = new();

    /// <summary>Outils impossibles en SPC (serveur Azure partagé) : sauvegarde, restauration, copie des utilisateurs,
    /// optimisation, fichiers de connexion, mot de passe administrateur, sessions fantômes.</summary>
    private readonly HashSet<Button> _spcUnavailableButtons = new();
    private bool _updatingCompanies;

    public MainWindow()
    {
        InitializeComponent();
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);

        LogItemsControl.ItemsSource = _logEntries;
        Logger.Instance.EntryLogged += OnLogEntry;

        _databaseButtons.AddRange(new[]
        {
            BtnListTriggers, BtnListingRfe, BtnIntegrityCompta, BtnIntegrityCial,
            BtnDecloture, BtnDevaliderFacture, BtnCreerInfoLibre, BtnEnrichissement, BtnRecodification, BtnCorrigerAdresses, BtnCorrigerAccents,
            BtnResetAdminPwd, BtnPurgeGhosts
        });
        _instanceButtons.AddRange(new[]
        {
            BtnOptimize, BtnGenConnFiles, BtnCopyUsers, BtnRestoreSql, BtnBackupAll, BtnAuditTva
        });
        _spcUnavailableButtons.UnionWith(new[]
        {
            BtnOptimize, BtnGenConnFiles, BtnCopyUsers, BtnRestoreSql, BtnBackupAll, BtnResetAdminPwd, BtnPurgeGhosts
        });
        RefreshButtonStates();

        InstanceInput.Text = ConfigManager.Instance.Config.LastInstance ?? string.Empty;
        DatabaseInput.Text = ConfigManager.Instance.Config.LastDatabase ?? string.Empty;
        SpcServerInput.Text = ConfigManager.Instance.Config.SpcServer ?? string.Empty;
        SpcDatabaseInput.Text = ConfigManager.Instance.Config.SpcDatabase ?? string.Empty;
        SpcUserInput.Text = ConfigManager.Instance.Config.SpcUser ?? string.Empty;
        if (ConfigManager.Instance.Config.ConnectionMode == "Spc") ModeSpcRadio.IsChecked = true;

        var savedTheme = ThemeManager.ParseSaved(ConfigManager.Instance.Config.Theme);
        ThemeCombo.SelectedIndex = savedTheme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0
        };

        Closed += (_, _) => Logger.Instance.EntryLogged -= OnLogEntry;

        Logger.Instance.Info("Sage Toolbox démarré.");
        Loaded += async (_, _) => await DetectSpcAsync(automatic: true);
    }

    private void OnLogEntry(LogEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            _logEntries.Add(entry);
            if (_logEntries.Count > 2000)
                _logEntries.RemoveAt(0);
            LogScrollViewer.ScrollToEnd();
        });
    }

    /// <summary>Désactive tous les boutons dépendant d'une connexion (pendant une action en cours).</summary>
    private void DisableAllConnectionButtons()
    {
        foreach (var button in _databaseButtons) button.IsEnabled = false;
        foreach (var button in _instanceButtons) button.IsEnabled = false;
    }

    /// <summary>Réactive les boutons en fonction de l'état de connexion courant : les
    /// boutons "Serveur &amp; Instance" dès qu'une instance est connectée, les boutons
    /// niveau base seulement si une base précise est aussi sélectionnée.</summary>
    private void RefreshButtonStates()
    {
        var instanceConnected = DatabaseManager.Instance.CurrentInstance is not null;
        var databaseConnected = DatabaseManager.Instance.CurrentDatabase is not null;

        foreach (var button in _instanceButtons) button.IsEnabled = instanceConnected && !IsBlockedInSpc(button);
        foreach (var button in _databaseButtons) button.IsEnabled = databaseConnected && !IsBlockedInSpc(button);
    }

    /// <summary>Vrai (et infobulle explicative) si l'outil n'existe pas en SPC et que la connexion courante est SPC.</summary>
    private bool IsBlockedInSpc(Button button)
    {
        var blocked = DatabaseManager.Instance.IsSpc && _spcUnavailableButtons.Contains(button);
        if (blocked) button.ToolTip = $"{button.Content} — non disponible en mode SPC";
        else button.ClearValue(ToolTipProperty);
        return blocked;
    }

    // ----- Connexion -----

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var instance = InstanceInput.Text.Trim();
        var database = DatabaseInput.Text.Trim();
        if (string.IsNullOrEmpty(instance))
        {
            Logger.Instance.Warning("Merci de renseigner l'instance SQL.");
            return;
        }

        if (string.IsNullOrEmpty(database))
            await ConnectInstanceOnlyAsync(instance);
        else
            await ConnectAsync(instance, database);
    }

    private void OpenMaeButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Fichier de paramétrage Sage (*.mae)|*.mae|Tous les fichiers (*.*)|*.*",
            Title = "Ouvrir un fichier .mae"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var content = File.ReadAllText(dialog.FileName);
            var match = Regex.Match(content, @"^\s*ServeurSQL\s*=\s*(.+)$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                Logger.Instance.Error("Impossible de trouver 'ServeurSQL=' dans le fichier .mae sélectionné.");
                return;
            }

            var instance = match.Groups[1].Value.Trim();
            var database = Path.GetFileNameWithoutExtension(dialog.FileName);

            InstanceInput.Text = instance;
            DatabaseInput.Text = database;
            _ = ConnectAsync(instance, database);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Lecture du fichier .mae impossible : {ex.Message}");
        }
    }

    private async Task ConnectAsync(string instance, string database)
    {
        ClosePanel();
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            Logger.Instance.Info($"Connexion à {instance} / {database}...");
            var (success, error) = await DatabaseManager.Instance.TestConnectionAsync(instance, database);
            if (!success)
            {
                Logger.Instance.Error($"Échec de connexion : {error}");
                return;
            }

            DatabaseManager.Instance.Connect(instance, database);
            ConfigManager.Instance.Config.ConnectionMode = "OnPremise";
            ConfigManager.Instance.SaveLastConnection(instance, database);
            Logger.Instance.Success($"Connecté à {database} sur {instance}.");
            RefreshButtonStates();

            DbNameText.Text = database;
            DbProductsText.Text = "Chargement des informations...";
            DbSizeText.Text = string.Empty;
            DbPathText.Text = string.Empty;

            var info = await _serverRepository.GetDatabaseInfoAsync(instance, database);
            DbProductsText.Text = string.Join(", ", info.InstalledProducts());
            if (string.IsNullOrEmpty(DbProductsText.Text))
                DbProductsText.Text = "Aucun module Sage détecté";
            DbSizeText.Text = $"Data: {info.DataSizeMb} Mo  |  Log: {info.LogSizeMb} Mo" +
                               (info.FileStreamSizeMb > 0 ? $"  |  FS: {info.FileStreamSizeMb} Mo" : string.Empty);
            DbPathText.Text = info.PhysicalPath;

            await LoadInstanceInfoAsync(instance);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Erreur de connexion : {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    /// <summary>Charge et affiche les informations techniques de l'instance SQL (version,
    /// édition, authentification, répertoires, FILESTREAM) dans le bandeau latéral.</summary>
    private async Task LoadInstanceInfoAsync(string instance)
    {
        try
        {
            var info = await _serverRepository.GetInstanceInfoAsync(instance);

            InstanceNotConnectedText.Visibility = Visibility.Collapsed;
            foreach (var tb in new[]
            {
                InstanceVersionText, InstanceEditionText, InstanceAuthText,
                InstanceFilestreamText, InstanceDataPathText, InstanceLogPathText, InstanceBackupPathText
            })
                tb.Visibility = Visibility.Visible;

            InstanceVersionText.Text = info.VersionLabel;
            InstanceEditionText.Text = info.Edition;
            InstanceAuthText.Text = $"Authentification : {info.AuthModeLabel}";
            InstanceFilestreamText.Text = $"FILESTREAM : {info.FilestreamLabel}";
            InstanceDataPathText.Text = $"Data : {info.DataPath}";
            InstanceLogPathText.Text = $"Log : {info.LogPath}";
            InstanceBackupPathText.Text = $"Backup : {info.BackupPath}";
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"Impossible de lire les informations de l'instance : {ex.Message}");
        }
    }

    /// <summary>Connexion à l'instance seule, sans base sélectionnée — n'active que les
    /// fonctions "Serveur &amp; Instance".</summary>
    private async Task ConnectInstanceOnlyAsync(string instance)
    {
        ClosePanel();
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            Logger.Instance.Info($"Connexion à l'instance {instance} (sans base sélectionnée)...");
            var (success, error) = await DatabaseManager.Instance.TestConnectionAsync(instance, "master");
            if (!success)
            {
                Logger.Instance.Error($"Échec de connexion : {error}");
                return;
            }

            DatabaseManager.Instance.ConnectInstanceOnly(instance);
            ConfigManager.Instance.Config.ConnectionMode = "OnPremise";
            ConfigManager.Instance.Config.LastInstance = instance;
            ConfigManager.Instance.Save();
            Logger.Instance.Success($"Connecté à l'instance {instance} (fonctions Serveur & Instance disponibles).");
            RefreshButtonStates();

            DbNameText.Text = "Aucune base sélectionnée";
            DbProductsText.Text = "Connecté à l'instance uniquement";
            DbSizeText.Text = string.Empty;
            DbPathText.Text = string.Empty;

            await LoadInstanceInfoAsync(instance);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Erreur de connexion : {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    // ----- Connexion SPC (Sage Premium Cloud) -----

    private void ConnectionMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded && OnPremisePanel is null) return;
        var spc = ModeSpcRadio.IsChecked == true;
        OnPremisePanel.Visibility = spc ? Visibility.Collapsed : Visibility.Visible;
        SpcPanel.Visibility = spc ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SpcDetectButton_Click(object sender, RoutedEventArgs e) => await DetectSpcAsync(automatic: false);

    /// <summary>Cherche une configuration SPC sur le poste (registre Sage 100) et, si elle existe, passe en mode SPC,
    /// renseigne les champs et se connecte. Au démarrage (automatic), un poste sans SPC reste silencieux.</summary>
    private async Task DetectSpcAsync(bool automatic)
    {
        var found = await Task.Run(SpcDetector.Detect);
        if (found.Count == 0)
        {
            if (!automatic) Logger.Instance.Warning("SPC : aucun environnement Sage Partner Cloud détecté sur ce poste.");
            return;
        }

        var saved = ConfigManager.Instance.Config.SpcDatabase;
        var db = found.FirstOrDefault(d => string.Equals(d.Database, saved, StringComparison.OrdinalIgnoreCase)) ?? found[0];
        Logger.Instance.Info($"Environnement SPC détecté : {db.Server} / {db.Database}.");
        if (found.Count > 1)
            Logger.Instance.Info($"{found.Count} bases SPC détectées ({string.Join(", ", found.Select(d => d.Database))}) : {db.Database} utilisée.");

        ModeSpcRadio.IsChecked = true;
        SpcServerInput.Text = db.Server;
        SpcDatabaseInput.Text = db.Database;
        SpcUserInput.Text = db.Login;
        SpcPasswordBox.Password = db.Password;
        await ConnectSpcAsync(DatabaseManager.BuildSpcConnectionString(db.Server, db.Database, db.Login, db.Password));
    }

    private async void SpcConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var server = SpcServerInput.Text.Trim();
        var database = SpcDatabaseInput.Text.Trim();
        var user = SpcUserInput.Text.Trim();
        if (server.Length == 0 || database.Length == 0 || user.Length == 0 || SpcPasswordBox.Password.Length == 0)
        {
            Logger.Instance.Warning("SPC : merci de renseigner le serveur, la base, l'utilisateur et le mot de passe.");
            return;
        }

        await ConnectSpcAsync(DatabaseManager.BuildSpcConnectionString(server, database, user, SpcPasswordBox.Password));
    }

    /// <summary>Connexion au serveur Azure SQL d'un dossier SPC puis lecture des sociétés (cbase.companies).
    /// Une société unique est sélectionnée d'office ; sinon l'utilisateur la choisit dans la liste.</summary>
    private async Task ConnectSpcAsync(Microsoft.Data.SqlClient.SqlConnectionStringBuilder builder)
    {
        ClosePanel();
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            Logger.Instance.Info($"Connexion SPC à {builder.DataSource} / {builder.InitialCatalog}...");
            var companies = await DatabaseManager.Instance.ConnectSpcAsync(builder);

            var config = ConfigManager.Instance.Config;
            config.ConnectionMode = "Spc";
            config.SpcServer = builder.DataSource;
            config.SpcDatabase = builder.InitialCatalog;
            config.SpcUser = builder.UserID;
            ConfigManager.Instance.Save();

            _updatingCompanies = true;
            SpcCompanyCombo.ItemsSource = companies;
            SpcCompanyCombo.SelectedIndex = -1;
            _updatingCompanies = false;
            SpcCompanyPanel.Visibility = Visibility.Visible;

            Logger.Instance.Success($"Connecté au serveur SPC {builder.DataSource} : {companies.Count} société(s).");
            ShowSpcInstanceInfo(companies.Count);
            DbNameText.Text = "Aucune société sélectionnée";
            DbProductsText.Text = companies.Count == 0 ? "Aucune société trouvée (cbase.companies vide)" : "Choisissez une société dans la liste";
            DbSizeText.Text = string.Empty;
            DbPathText.Text = string.Empty;
            RefreshButtonStates();

            if (companies.Count == 1) SpcCompanyCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Échec de connexion SPC : {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async void SpcCompanyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingCompanies || SpcCompanyCombo.SelectedItem is not SpcCompany company) return;

        ClosePanel();
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            DatabaseManager.Instance.SelectSpcCompany(company);
            Logger.Instance.Success($"Société SPC sélectionnée : {company.Name} (schéma {company.SchemaName}).");
            RefreshButtonStates();

            DbNameText.Text = company.Name;
            DbProductsText.Text = "Chargement des informations...";
            DbSizeText.Text = $"Schéma : {company.SchemaName}";
            DbPathText.Text = $"Mode SPC — base {DatabaseManager.Instance.SpcDatabase}";

            var info = await _serverRepository.GetSpcCompanyInfoAsync(company);
            DbProductsText.Text = string.Join(", ", info.InstalledProducts());
            if (string.IsNullOrEmpty(DbProductsText.Text))
                DbProductsText.Text = "Aucun module Sage détecté";
        }
        catch (Exception ex)
        {
            DbProductsText.Text = "Informations indisponibles";
            Logger.Instance.Warning($"Société SPC {company.Name} : lecture des informations impossible — {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    /// <summary>Bandeau « Info. Instance SQL » en SPC : pas de version, de chemins ni de FILESTREAM côté serveur Azure.</summary>
    private void ShowSpcInstanceInfo(int companyCount)
    {
        InstanceNotConnectedText.Visibility = Visibility.Collapsed;
        foreach (var tb in new[] { InstanceVersionText, InstanceEditionText, InstanceAuthText, InstanceFilestreamText })
            tb.Visibility = Visibility.Visible;
        foreach (var tb in new[] { InstanceDataPathText, InstanceLogPathText, InstanceBackupPathText })
            tb.Visibility = Visibility.Collapsed;

        InstanceVersionText.Text = "Sage Premium Cloud (SPC)";
        InstanceEditionText.Text = $"Serveur : {DatabaseManager.Instance.CurrentInstance}";
        InstanceAuthText.Text = $"Base : {DatabaseManager.Instance.SpcDatabase} — authentification SQL";
        InstanceFilestreamText.Text = $"{companyCount} société(s)";
    }

    // ----- Panneaux d'outils (zone centrale) -----

    private void OpenPanel(ToolPanel panel)
    {
        panel.CloseRequested += (_, _) => ClosePanel();
        ToolHostTitle.Text = panel.Title;
        ToolContent.Content = panel;
        TilesScroll.Visibility = Visibility.Collapsed;
        ToolHost.Visibility = Visibility.Visible;
    }

    private void ClosePanel()
    {
        ToolHost.Visibility = Visibility.Collapsed;
        ToolContent.Content = null;
        TilesScroll.Visibility = Visibility.Visible;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => ClosePanel();

    // ----- Thème -----

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var theme = ThemeCombo.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System
        };
        ThemeManager.Apply(theme);
        WindowChromeHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentEffectiveTheme == AppTheme.Dark);
    }

    // ----- Actions génériques -----

    private static bool Confirm(string action, string? extraWarning = null)
    {
        var message = extraWarning is null ? $"Lancer \"{action}\" ?" : $"Lancer \"{action}\" ?\n\n{extraWarning}";
        return MessageBox.Show(message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private async Task RunActionAsync(string label, Func<Task> work, string? confirmWarning = null)
    {
        if (!Confirm(label, confirmWarning)) return;

        DisableAllConnectionButtons();
        Mouse.OverrideCursor = Cursors.Wait;
        Logger.Instance.Info($"{label}...");
        try
        {
            await work();
            Logger.Instance.Success($"{label} : terminé.");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"{label} : échec — {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
            RefreshButtonStates();
        }
    }

    // ----- Maintenance technique -----

    /// <summary>Ouvre le choix de portée (base connectée ou toutes les bases) d'un outil, puis lance
    /// l'action correspondante après fermeture du panneau.</summary>
    private void OpenScopePanel(string title, string description, string actionLabel,
        Func<Task> forDatabase, Func<Task> forAllDatabases)
    {
        var panel = new ScopePanel(title, description, actionLabel);
        panel.ScopeChosen += async (_, all) =>
        {
            ClosePanel();
            await (all ? forAllDatabases() : forDatabase());
        };
        OpenPanel(panel);
    }

    private void BtnOptimize_Click(object sender, RoutedEventArgs e) =>
        OpenScopePanel("Optimiser",
            "Applique le réglage standard : fermeture automatique désactivée, mode de récupération « Simple », niveau de compatibilité aligné sur la version du serveur.",
            "Optimiser", OptimizeDatabaseAsync, OptimizeAllAsync);

    private void BtnGenConnFiles_Click(object sender, RoutedEventArgs e) =>
        OpenScopePanel("Fichiers de connexion",
            "Génère les fichiers de connexion Sage (.mae, .gcm, .mdp, .imo, .prh selon les modules installés) dans un dossier à choisir.",
            "Générer", GenerateConnectionFilesAsync, GenerateAllConnectionFilesAsync);

    private async Task OptimizeDatabaseAsync()
    {
        var instance = DatabaseManager.Instance.CurrentInstance!;
        var database = DatabaseManager.Instance.CurrentDatabase!;
        await RunActionAsync("Optimiser la base", async () =>
        {
            var sessions = await _serverRepository.GetActiveConnectionsAsync(instance, database);
            if (sessions.Count > 0)
            {
                Logger.Instance.Warning($"{sessions.Count} utilisateur(s) connecté(s) à \"{database}\" pendant l'optimisation :");
                foreach (var s in sessions) Logger.Instance.Info($"    {s}");
            }
            await _serverRepository.OptimizeDatabaseAsync(instance, database);
        });
    }

    private async Task GenerateConnectionFilesAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Dossier de destination" };
        if (dialog.ShowDialog() != true) return;

        var instance = DatabaseManager.Instance.CurrentInstance!;
        var database = DatabaseManager.Instance.CurrentDatabase!;
        await RunActionAsync("Générer fichiers de connexion", async () =>
        {
            var files = await _connectionFileRepository.GenerateAsync(instance, database, dialog.FolderName);
            foreach (var f in files) Logger.Instance.Info($"  {f}");
            if (files.Count == 0) Logger.Instance.Warning("Aucun fichier généré (ni T_INSTANCES, ni cbsystable détectées).");
        });
    }

    private async void BtnListTriggers_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Lister les triggers spécifiques", async () =>
        {
            var triggers = await _serverRepository.ListNonStandardTriggersAsync();
            if (triggers.Count == 0)
            {
                Logger.Instance.Info("Aucun trigger non standard détecté.");
                return;
            }
            foreach (var t in triggers)
                Logger.Instance.Info($"  {t.TableName} -> {t.TriggerName}");
            Logger.Instance.Warning($"{triggers.Count} trigger(s) non standard détecté(s).");
        });
    }

    private async void BtnIntegrityCompta_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Contrôle d'intégrité comptable", async () =>
        {
            var issues = await _integrityRepository.RunAllAsync();
            LogIntegrityIssues(issues, "Comptable");
        });
    }

    private async void BtnIntegrityCial_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Contrôle d'intégrité gestion commerciale", async () =>
        {
            var issues = await _commercialIntegrityRepository.RunAllAsync();
            LogIntegrityIssues(issues, "GestionCommerciale");
        });
    }

    private void LogIntegrityIssues(List<IntegrityIssue> issues, string reportName)
    {
        if (issues.Count == 0)
        {
            Logger.Instance.Info("Aucune anomalie détectée.");
            return;
        }

        foreach (var group in issues.GroupBy(i => i.Controle))
        {
            var items = group.ToList();
            Logger.Instance.Warning($"{group.Key} : {items.Count} anomalie(s)");
            foreach (var issue in items.Take(20))
                Logger.Instance.Info($"    {issue.Detail}");
            if (items.Count > 20)
                Logger.Instance.Info($"    ... et {items.Count - 20} de plus.");
        }

        WriteIntegrityReportFile(issues, reportName);
    }

    /// <summary>Écrit un fichier texte détaillé à l'emplacement de l'exécutable, pour archivage/partage.</summary>
    private static void WriteIntegrityReportFile(List<IntegrityIssue> issues, string reportName)
    {
        try
        {
            var database = DatabaseManager.Instance.CurrentDatabase ?? "base";
            var fileName = $"ControleIntegrite_{reportName}_{database}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, fileName);

            var lines = new List<string>
            {
                $"Contrôle d'intégrité {reportName} — base {database} — {DateTime.Now:dd/MM/yyyy HH:mm:ss}",
                $"{issues.Count} anomalie(s) au total.",
                string.Empty
            };
            foreach (var group in issues.GroupBy(i => i.Controle))
            {
                var items = group.ToList();
                lines.Add($"{group.Key} : {items.Count} anomalie(s)");
                lines.AddRange(items.Select(issue => $"    {issue.Detail}"));
                lines.Add(string.Empty);
            }

            System.IO.File.WriteAllLines(path, lines);
            Logger.Instance.Info($"Rapport détaillé écrit : {path}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"Impossible d'écrire le fichier de rapport : {ex.Message}");
        }
    }

    // ----- Opérations (Base) -----

    private void BtnDecloture_Click(object sender, RoutedEventArgs e)
    {
        OpenPanel(new DeclotureWindow());
    }

    private void BtnDevaliderFacture_Click(object sender, RoutedEventArgs e)
    {
        OpenPanel(new DevaliderFactureWindow());
    }

    private void BtnCreerInfoLibre_Click(object sender, RoutedEventArgs e)
    {
        OpenPanel(new CreateInfoLibreWindow());
    }

    private async void BtnCorrigerAdresses_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Corriger les adresses de livraison", async () =>
        {
            var created = await _tiersRepository.CorrigerAdressesLivraisonAsync();
            Logger.Instance.Info($"{created} adresse(s) de livraison créée(s), documents orphelins rattachés.");
        });
    }

    private async void BtnCorrigerAccents_Click(object sender, RoutedEventArgs e)
    {
        var scan = await _serverRepository.ScanEncodingIssuesAsync();
        if (scan.Count == 0)
        {
            Logger.Instance.Info("Corriger les accents : aucune anomalie d'encodage détectée.");
            return;
        }

        var detail = string.Join("\n", scan.Select(kv => $"  {kv.Key} : {kv.Value} ligne(s)"));
        var message = $"Tables impactées :\n{detail}\n\nAppliquer la correction ?";
        if (MessageBox.Show(message, "Corriger les accents", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;

        await RunActionAsync("Corriger les accents", async () =>
        {
            var updated = await _serverRepository.FixEncodingAsync();
            Logger.Instance.Info($"{updated} ligne(s) corrigée(s).");
        });
    }

    // ----- Administration (Base) -----

    private void BtnResetAdminPwd_Click(object sender, RoutedEventArgs e)
    {
        OpenPanel(new ResetAdminPasswordWindow());
    }

    private async void BtnPurgeGhosts_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Déconnexion des utilisateurs fantômes", async () =>
        {
            var ghosts = await _adminRepository.PurgeGhostSessionsAsync();
            Logger.Instance.Info(ghosts.Count == 0
                ? "Aucun utilisateur fantôme à déconnecter."
                : $"Utilisateurs fantômes déconnectés : {string.Join(", ", ghosts)}");
        });
    }

    // ----- Serveur & Instance -----

    private async Task OptimizeAllAsync()
    {
        var instance = DatabaseManager.Instance.CurrentInstance!;
        await RunActionAsync("Optimiser TOUTES les bases", async () =>
        {
            var databases = await _serverRepository.DiscoverSageDatabasesAsync(instance);
            Logger.Instance.Info($"{databases.Count} base(s) Sage détectée(s) sur l'instance.");
            await using var conn = await DatabaseManager.Instance.OpenAutocommitConnectionAsync(instance, "master");
            foreach (var db in databases)
            {
                var sessions = await _serverRepository.GetActiveConnectionsAsync(instance, db);
                if (sessions.Count > 0)
                    Logger.Instance.Warning($"  {db} : {sessions.Count} utilisateur(s) connecté(s) pendant l'optimisation.");

                Logger.Instance.Info($"  Optimisation de {db}...");
                await _serverRepository.OptimizeDatabaseAsync(instance, db, conn);
            }
        });
    }

    private async Task GenerateAllConnectionFilesAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Dossier de destination" };
        if (dialog.ShowDialog() != true) return;

        var instance = DatabaseManager.Instance.CurrentInstance!;
        await RunActionAsync("Générer TOUS les fichiers de connexion", async () =>
        {
            var databases = await _serverRepository.DiscoverSageDatabasesAsync(instance);
            foreach (var db in databases)
            {
                var files = await _connectionFileRepository.GenerateAsync(instance, db, dialog.FolderName);
                Logger.Instance.Info($"  {db} : {files.Count} fichier(s) généré(s).");
            }
        });
    }

    private void BtnCopyUsers_Click(object sender, RoutedEventArgs e)
    {
        OpenPanel(new CopyUsersWindow());
    }

    private void BtnRestoreSql_Click(object sender, RoutedEventArgs e)
    {
        OpenPanel(new RestorationWindow());
    }

    private void BtnBackupAll_Click(object sender, RoutedEventArgs e)
    {
        OpenPanel(new BackupAllWindow());
    }

    // ----- Audit -----

    private void BtnAuditTva_Click(object sender, RoutedEventArgs e) => OpenPanel(new AuditTvaPanel());

    private void BtnConformity_Click(object sender, RoutedEventArgs e) => OpenPanel(new ConformityPanel());

    private void BtnListingRfe_Click(object sender, RoutedEventArgs e) => OpenPanel(new ListingRfePanel());

    private void BtnEnrichissement_Click(object sender, RoutedEventArgs e) => OpenPanel(new EnrichissementPanel());

    private void BtnRecodification_Click(object sender, RoutedEventArgs e) => OpenPanel(new RecodificationPanel());
}
