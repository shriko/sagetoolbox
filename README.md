# Sage Toolbox

Application desktop Windows interne pour l'administration de bases **Sage 100** (Comptabilité, Gestion Commerciale, Trésorerie, Immobilisations) hébergées sur **SQL Server**. Connexion en authentification Windows intégrée, lecture/écriture directe dans les tables Sage (pas d'API Sage officielle).

Réécriture en **C# / WPF (.NET 8)** de l'outil historique `AIG_Toolbox.py` (Python / Tkinter, conservé à la racine du dépôt à titre de référence).

---

## Fonctionnalités

### Connexion
- Connexion manuelle (instance SQL + base) ou via fichier `.mae`
- Mémorisation de la dernière connexion
- Panneau d'information sur la base connectée (modules Sage installés, tailles des fichiers, chemin physique)

### Maintenance (base)
- **Optimiser la base** : `AUTO_CLOSE OFF`, `RECOVERY SIMPLE`, niveau de compatibilité aligné sur la version réelle de SQL Server, activation FILESTREAM (accès complet, répertoire = nom de la base), correction des noms logiques de fichiers (alignés sur le nom de la base), réassociation `USER_CBASE` → `APPL_CBASE`. Bloque l'opération si des utilisateurs sont connectés à la base.
- Génération des fichiers de connexion (`.prh` / `.mae` / `.gcm` / `.mdp` / `.imo`)
- Liste des triggers non standards
- Contrôle d'intégrité comptable (comptes/journaux manquants, équilibre par journal et par mois, écritures analytiques orphelines ou invalides, comptes attendant une ventilation analytique) — génère un rapport `.txt` à côté de l'exécutable en cas d'anomalie
- Contrôle d'intégrité gestion commerciale (tiers, dépôts, collaborateurs, lignes orphelines)

### Opérations (base)
- Déclôture d'exercice comptable, avec sauvegarde préalable optionnelle et blocage si un exercice plus récent est encore clôturé
- Dévalidation d'une facture (avec désambiguïsation si plusieurs documents partagent le même numéro de pièce)
- Création d'une Information Libre (avec miroir automatique F_DOCENTETE→F_ABOENTETE et F_DOCLIGNE→F_ABOLIGNE)
- Correction des adresses de livraison manquantes
- Correction des accents mal encodés

### Administration (base)
- Suppression du mot de passe administrateur, avec désactivation optionnelle des règles de politique de mot de passe (complexité, renouvellement, etc.)
- Déconnexion des utilisateurs fantômes

### Serveur & instance
- Optimisation de toutes les bases Sage de l'instance
- Génération des fichiers de connexion pour toutes les bases
- Copie des utilisateurs/droits entre deux bases

### Transverse
- Console de log temps réel + fichier persistant (`%APPDATA%\SageToolbox\sage_toolbox.log`)
- Thème Système / Clair / Sombre (y compris la barre de titre native Windows)
- Confirmation systématique avant toute action, exécution asynchrone

---

## Stack technique

- **C# / .NET 8**, **WPF** pour l'interface
- **Microsoft.Data.SqlClient** pour l'accès SQL Server (authentification Windows intégrée, `Integrated Security=true`)
- Packagé en exécutable autonome (`self-contained`, `PublishSingleFile`) — aucune installation de runtime requise sur le poste cible

## Architecture

```
src/SageToolbox/
├── Core/            → Logger, ConfigManager, DatabaseManager, ThemeManager, PasswordHasher, WindowChromeHelper
├── Models/          → Objets de données (ExerciceInfo, SageDatabaseInfo, IntegrityIssue, ...)
├── Repositories/     → Un repository par domaine métier, toute la logique SQL
├── Views/           → Fenêtres secondaires (modales) + thèmes XAML (Views/Themes)
├── MainWindow.xaml* → Fenêtre principale (sidebar + tableau de bord + console de log)
└── App.xaml*        → Point d'entrée, démarrage (authentification → fenêtre principale)
```

Chaque fonctionnalité métier vit dans son `Repository` (`ServerRepository`, `AccountingRepository`, `AdminRepository`, `TiersRepository`, `DocumentRepository`, `StructureRepository`, `UsersRepository`, `IntegrityRepository`, `CommercialIntegrityRepository`, `ConnectionFileRepository`), indépendamment de l'UI.

## Prérequis

- Windows avec accès à une instance SQL Server hébergeant des bases Sage 100
- Pour compiler : [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Démarrage rapide (développement)

```powershell
dotnet run --project src/SageToolbox/SageToolbox.csproj
```

## Publication (exécutable autonome)

```powershell
dotnet publish src/SageToolbox/SageToolbox.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

L'exécutable est généré dans `src/SageToolbox/bin/Release/net8.0-windows/win-x64/publish/SageToolbox.exe` — il suffit de le copier sur le poste cible, aucune installation requise.

## Configuration

- **Mot de passe applicatif** : mécanisme identique à l'outil historique (hash MD5 codé en dur dans `Core/PasswordHasher.cs`). À changer avant toute diffusion externe à l'équipe.
- **Configuration utilisateur** (dernière connexion, thème) : `%APPDATA%\SageToolbox\config.json`, avec migration automatique depuis l'ancien emplacement `%APPDATA%\AIG_Toolbox` si présent.

## Points d'attention

- Certaines valeurs (colonnes de correction d'accents, format des fichiers de connexion) ont été portées depuis le code source Python d'origine et vérifiées sur des bases réelles ; se référer aux commentaires en tête de chaque `Repository` pour le détail des hypothèses et vérifications effectuées.
- Le contrôle d'intégrité comptable plafonne chaque sous-contrôle à 200 lignes remontées (anti-inondation de l'UI) ; le rapport texte généré reflète ce même plafond.

## Historique

- `AIG_Toolbox.py` : version Python/Tkinter d'origine (conservée pour référence)
- `SYNTHESE_V2.md` : synthèse fonctionnelle ayant servi de base à la réécriture
