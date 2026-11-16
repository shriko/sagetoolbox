# Sage Toolbox

Application desktop Windows interne pour l'administration de bases **Sage 100** (Comptabilité, Gestion Commerciale, Trésorerie, Immobilisations) hébergées sur **SQL Server**. Connexion en authentification Windows intégrée, lecture/écriture directe dans les tables Sage (pas d'API Sage officielle).

Application **C# / WPF (.NET 10)**. Le guide d'utilisation est dans [docs/GUIDE_UTILISATEUR.md](docs/GUIDE_UTILISATEUR.md). L'application vérifie au démarrage la présence d'une nouvelle release GitHub et propose de l'installer.

---

## Fonctionnalités

### Connexion
- Connexion manuelle (instance SQL + base) ou via fichier `.mae`
- Connexion à une **instance seule**, sans base sélectionnée (champ base vide) — n'active alors que les outils marqués « Instance »
- **Connexion SPC (Sage Premium Cloud)** : serveur Azure SQL, base, utilisateur et mot de passe SQL (jamais enregistré). L'outil lit la liste des sociétés (`cbase.companies`) ; la société choisie détermine le schéma SQL (`name` = bijou, `schemaName` = soc_bijou → `soc_bijou.F_ECRITUREC`), seul élément qui change par rapport à Sage 100 sur SQL Server (`dbo.F_ECRITUREC`). Indisponibles en SPC (tuiles grisées) : sauvegarde et restauration, copie des utilisateurs, optimisation, fichiers de connexion, suppression du mot de passe administrateur, déconnexion des utilisateurs fantômes. Les autres outils fonctionnent sur la société sélectionnée ; l'Audit TVA liste les sociétés du dossier, et la déclôture n'offre pas de sauvegarde préalable
- Mémorisation de la dernière connexion (mode, serveur, base, utilisateur)
- Panneau d'information sur la base connectée (modules Sage installés, tailles des fichiers, chemin physique)
- Panneau d'information sur l'**instance SQL** (version, édition/architecture, mode d'authentification, état FILESTREAM, répertoires par défaut data/log/backup)

### Organisation de l'écran
- Colonne de gauche toujours visible : connexion, informations sur la base et l'instance, thème
- Zone centrale : outils présentés en **tuiles** (icône + titre), regroupés par nature d'action ; un outil qui demande une saisie s'ouvre dans la zone centrale avec un bouton « ← Retour »
- Badge **Instance** sur les outils qui n'ont besoin que de la connexion à l'instance (pas de base sélectionnée) ; liseré rouge sur les outils sensibles (déclôture, dévalidation, recodification, suppression du mot de passe administrateur, restauration)
- Journal en bas, visible en permanence

### Contrôle & audit (lecture seule)
- Liste des triggers non standards
- Contrôle d'intégrité comptable (comptes/journaux manquants, équilibre par journal et par mois, écritures analytiques orphelines ou invalides, comptes attendant une ventilation analytique) — génère un rapport `.txt` à côté de l'exécutable en cas d'anomalie
- Contrôle d'intégrité gestion commerciale (tiers, dépôts, collaborateurs, lignes orphelines)
- **Audit TVA** : audit du paramétrage et de l'usage de la TVA sur les bases Sage Comptabilité de l'instance (10 contrôles par base : paramétrage société, journaux, comptes de TVA, codes taxe, rattachements, écritures sans code taxe…), avec détection d'anomalies et génération d'un rapport **Word, PDF et/ou Excel** (un rapport global ou un par base). Les contrôles 4.8 à 4.11 sont des heuristiques : une anomalie signalée est un point à vérifier. Les rapports PDF utilisent la licence *Community* de QuestPDF (gratuite sous 1 M$ de chiffre d'affaires annuel).

- **Listing RFE** : liste des adresses de facturation électronique (« maillage » : SIREN, SIRET, SIRET + code routage, SIREN + suffixe) des clients actifs de la base connectée (Sage 100 v12), regroupées par compte avec leurs contacts. Filtres (compte, raison sociale, actif, type de maille), choix des colonnes affichées, liens téléphone / e-mail, export Excel de la liste filtrée
- **Conformité des factures électroniques** : contrôle d'un fichier **XML UBL / CII / Factur-X / CDAR** ou d'un **PDF Factur-X** (XML extrait de la pièce jointe) contre les schematrons officiels **France_RFE (FNFE) 1.4.0.04**, avec détection automatique du profil (EN16931, EXTENDED-CTC-FR, Factur-X EN16931 / EXTENDED / BASIC WL, CDAR) ou profil forcé. Rapport règle par règle (conforme / non conforme / non applicable, détail de chaque occurrence en erreur, message de la norme), contrôle de structure XSD et points de vigilance (préfixe d'espace de noms racine, `#` superflu dans une note BT-22). Fonctionne sans connexion à une base ; les règles sont embarquées dans l'exécutable.

### Correction de données
- Déclôture d'exercice comptable : liste des exercices avec cadenas fermé (clôturé) / ouvert, sauvegarde préalable optionnelle, blocage si un exercice plus récent est encore clôturé, case de confirmation obligatoire
- **Dévalidation de factures** : liste des factures de vente validées non comptabilisées (n° de facture, date, n° et intitulé client, montant HT) avec sélection multiple et filtres (dates, client, n° de facture). Une facture déjà transmise en facturation électronique n'est pas dévalidable (elle existe côté Administration Fiscale : annuler et refaire une facture)
- Création d'une Information Libre sur les tables Sage qui l'acceptent (comptes, tiers, écritures, immobilisations, ressources, articles, numéros de série/lot, documents, abonnements), texte limité à 69 caractères comme dans Sage (avec miroir automatique F_DOCENTETE→F_ABOENTETE et F_DOCLIGNE→F_ABOLIGNE)
- **Enrichissement tiers** : fiabilise les SIRET / SIREN des fiches clients et fournisseurs en les rapprochant de l'API officielle « Recherche d'entreprises » (données INSEE / Sirene, accès internet requis). Statuts Valide, Trouvé, Multiples, A vérifier, Radié, Non trouvé, Erreur ou Ignoré (détection des établissements fermés et des déménagements via le SIREN), traitement concurrent avec limitation de débit et bouton Stop, choix manuel du candidat par double-clic, export Excel des lignes confirmées. Mise à jour de F_COMPTET au choix (SIRET, nom, adresse, n° de TVA, mise en sommeil des tiers radiés), protégée par des savepoints par fiche, avec journal d'audit des valeurs avant / après (`%APPDATA%\SageToolbox\mises_a_jour_tiers.txt`)
- **Recodification (bêta)** : changement en masse des codes des comptes généraux, comptes tiers, journaux, comptes analytiques (par plan), familles d'articles et articles, avec saisie dans la grille ou import / export Excel. Contrôles des nouveaux codes (longueur maximale, longueur imposée par le dossier, doublons, codes déjà existants), **fusion** d'une fiche dans une fiche existante de même nature, et traitement SQL en une seule transaction (annulation totale en cas d'erreur ou d'écart de contrôle) qui reporte le nouveau code dans toutes les tables Sage concernées. Contrôles préalables (aucun autre utilisateur, sauvegarde COPY_ONLY récente), rapport HTML détaillé dans `Documents\SageToolbox\Recodification`. Fonctionne en SPC : les tables sont préfixées du schéma de la société, et les sessions et la sauvegarde, inaccessibles sur le serveur Azure partagé, sont confirmées par l'utilisateur
- Correction des adresses de livraison manquantes
- Correction des accents mal encodés

### Utilisateurs & sécurité
- Copie des utilisateurs/droits entre deux bases
- Suppression du mot de passe administrateur, avec désactivation optionnelle des règles de politique de mot de passe (complexité, renouvellement, etc.)
- Déconnexion des utilisateurs fantômes

### Sauvegarde & restauration
- **Sauvegarde de toutes les bases** (hors bases système), avec option de compression
- **Restauration SQL en lot** : restaure tous les fichiers `.bak` d'un dossier, fichiers `.mdf`/`.ldf`/FILESTREAM redirigés automatiquement vers les répertoires par défaut de l'instance et nommés d'après la base restaurée (`{base}.mdf`, `{base}_log.ldf`, noms logiques `{base}`/`{base}_log`), FILESTREAM éventuel activé en accès complet (répertoire = nom de la base) ; chaque base restaurée est ensuite optimisée et ses fichiers de connexion générés automatiquement

### Maintenance technique
Les deux outils ci-dessous proposent le choix de la portée : la base connectée ou toutes les bases Sage de l'instance.
- **Optimiser** : `AUTO_CLOSE OFF`, `RECOVERY SIMPLE`, niveau de compatibilité aligné sur la version réelle de SQL Server, réassociation `USER_CBASE` → `APPL_CBASE`. Affiche un avertissement (sans bloquer) si des utilisateurs sont connectés à la base.
- **Fichiers de connexion** : génération des fichiers `.prh` / `.mae` / `.gcm` / `.mdp` / `.imo`

### Transverse
- Console de log temps réel + fichier persistant (`%APPDATA%\SageToolbox\sage_toolbox.log`)
- Thème Système / Clair / Sombre (y compris la barre de titre native Windows)
- Confirmation systématique avant toute action, exécution asynchrone
- Contrôle des utilisateurs connectés avant toute opération d'optimisation/restauration destructive

---

## Stack technique

- **C# / .NET 10**, **WPF** pour l'interface
- **Microsoft.Data.SqlClient** pour l'accès SQL Server (authentification Windows intégrée, `Integrated Security=true`)
- **Saxon-HE** ([SaxonCS-HE](https://www.nuget.org/packages/SaxonCS-HE), licence MPL 2.0) pour exécuter les schematrons France_RFE précompilés en XSLT 2.0 ; **PdfPig** (Apache 2.0) pour extraire le XML d'un PDF Factur-X ; **ClosedXML**, **DocumentFormat.OpenXml** et **QuestPDF** pour les rapports de l'audit TVA
- Règles **France_RFE (FNFE) 1.4.0.04** (licence Apache 2.0, `src/SageToolbox/Schematron/LICENSE-France_RFE.txt`) embarquées sous forme d'archive `fnfe_rules.zip` ; `build_rules_zip.py` la régénère à partir d'une nouvelle version des règles
- Packagé en exécutable autonome (`self-contained`, `PublishSingleFile`) — aucune installation de runtime requise sur le poste cible

## Architecture

```
src/SageToolbox/
├── Core/            → Logger, ConfigManager, DatabaseManager, ThemeManager, PasswordHasher, WindowChromeHelper
├── Models/          → Objets de données (ExerciceInfo, SageDatabaseInfo, IntegrityIssue, ...)
├── Repositories/     → Un repository par domaine métier, toute la logique SQL
├── Views/           → Écrans d'outils affichés dans la zone centrale (ToolPanel) + thèmes XAML (Views/Themes)
├── Tva/             → Audit TVA : contrôles SQL en lecture seule et générateurs de rapports Word / PDF / Excel
├── Recodification/  → Recodification : modèles de scripts SQL (Sql/), validation, exécution transactionnelle, rapport, import / export Excel
├── Rfe/             → Listing RFE : adresses de facturation électronique des clients, contacts, export Excel
├── Enrichissement/  → Enrichissement tiers : rapprochement SIRET / SIREN via l'API Recherche d'entreprises, mise à jour de F_COMPTET
├── Schematron/      → Contrôle de conformité des factures électroniques : règles embarquées, moteur Saxon, extraction Factur-X
├── MainWindow.xaml* → Fenêtre principale (sidebar + tableau de bord + console de log)
└── App.xaml*        → Point d'entrée, démarrage (authentification → fenêtre principale)
```

Chaque fonctionnalité métier vit dans son `Repository` (`ServerRepository`, `AccountingRepository`, `AdminRepository`, `TiersRepository`, `DocumentRepository`, `StructureRepository`, `UsersRepository`, `IntegrityRepository`, `CommercialIntegrityRepository`, `ConnectionFileRepository`, `RestorationRepository`, `BackupRepository`), indépendamment de l'UI.

## Prérequis

- Windows avec accès à une instance SQL Server hébergeant des bases Sage 100
- Pour compiler : [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

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
